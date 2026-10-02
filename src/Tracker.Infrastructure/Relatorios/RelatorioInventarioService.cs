using Microsoft.EntityFrameworkCore;
using Tracker.Application.Common;
using Tracker.Application.Relatorios;
using Tracker.Domain.Enums;
using Tracker.Infrastructure.Data;

namespace Tracker.Infrastructure.Relatorios;

public interface IRelatorioInventarioService
{
    Task<ResumoRelatoriosDto> ResumoAsync(CancellationToken ct = default);
    Task<IReadOnlyList<LinhaInventarioDto>> InventarioAsync(bool somenteAlugados, CancellationToken ct = default);
    Task<IReadOnlyList<LinhaUnidadeDto>> PorUnidadeAsync(CancellationToken ct = default);
    Task<(IReadOnlyList<LinhaTermoDto> Linhas, ResumoTermosDto Resumo)> TermosComputadoresAsync(CancellationToken ct = default);
}

/// <summary>
/// Monta os dados dos relatórios do painel. Lê sempre o parque ATIVO: o filtro global de consulta
/// já exclui os arquivados, então um ativo restaurado volta a contar e um arquivado nunca infla
/// o total. A classificação alugado/próprio sai de <see cref="ClassificacaoAluguel"/>.
/// </summary>
public class RelatorioInventarioService : IRelatorioInventarioService
{
    private readonly TrackerDbContext _db;

    public RelatorioInventarioService(TrackerDbContext db)
    {
        _db = db;
    }

    private static readonly Dictionary<StatusDispositivo, string> RotuloStatus = new()
    {
        [StatusDispositivo.EmUso] = "Em uso",
        [StatusDispositivo.EmEstoque] = "Em estoque",
        [StatusDispositivo.EmManutencao] = "Em manutenção",
        [StatusDispositivo.Bloqueado] = "Bloqueado",
        [StatusDispositivo.Baixado] = "Baixado",
    };

    private static readonly Dictionary<TipoDispositivo, string> RotuloTipo = new()
    {
        [TipoDispositivo.Notebook] = "Notebook",
        [TipoDispositivo.Mobile] = "Mobile",
        [TipoDispositivo.Periferico] = "Periférico",
        [TipoDispositivo.Impressora] = "Impressora",
    };

    /// <summary>
    /// Projeção comum a todos os relatórios. O hostname vive só nos tipos COM agente, e
    /// <c>OfType&lt;DispositivoComAgente&gt;()</c> não traduz para SQL (classe intermediária sem
    /// discriminador, ver CLAUDE.md) — por isso a projeção o busca por tipo concreto.
    /// </summary>
    private async Task<List<LinhaInventarioDto>> CarregarAsync(CancellationToken ct)
    {
        var hostnamesNotebook = await _db.Set<Domain.Entities.Notebook>().AsNoTracking()
            .Select(n => new { n.Id, n.Hostname })
            .ToDictionaryAsync(x => x.Id, x => x.Hostname, ct);

        var hostnamesMobile = await _db.Set<Domain.Entities.Mobile>().AsNoTracking()
            .Select(m => new { m.Id, m.Hostname })
            .ToDictionaryAsync(x => x.Id, x => x.Hostname, ct);

        // Impressora não segue o prefixo "MAQ": o aluguel dela é o interruptor Alugada.
        var impressorasAlugadas = await _db.Set<Domain.Entities.Impressora>().AsNoTracking()
            .Select(i => new { i.Id, i.Alugada })
            .ToDictionaryAsync(x => x.Id, x => x.Alugada, ct);

        var brutos = await _db.Dispositivos.AsNoTracking()
            .Select(d => new
            {
                d.Id,
                d.Tipo,
                d.IdentificacaoInterna,
                d.NumeroSerie,
                d.AssetTag,
                d.Status,
                d.CriadoEm,
                d.VencimentoAluguel,
                d.PertenceAUsuario,
                UnidadeNome = d.Unidade != null ? d.Unidade.Nome : null,
                UsuarioNome = d.UsuarioAtual != null ? d.UsuarioAtual.Nome : null,
            })
            .ToListAsync(ct);

        return brutos.Select(d =>
        {
            var hostname = hostnamesNotebook.TryGetValue(d.Id, out var hn) ? hn
                : hostnamesMobile.TryGetValue(d.Id, out var hm) ? hm
                : null;
            var identificacao = ClassificacaoAluguel.IdentificacaoExibida(d.IdentificacaoInterna, hostname, d.NumeroSerie);
            bool? impressoraAlugada = impressorasAlugadas.TryGetValue(d.Id, out var alugada) ? alugada : null;

            return new LinhaInventarioDto(
                Identificacao: identificacao,
                // Fallback para o nome do enum: um tipo novo sem rótulo não pode derrubar o relatório.
                Tipo: RotuloTipo.TryGetValue(d.Tipo, out var rotuloTipo) ? rotuloTipo : d.Tipo.ToString(),
                Origem: ClassificacaoAluguel.Rotulo(
                    ClassificacaoAluguel.EhAlugado(d.Tipo, impressoraAlugada, identificacao)),
                NumeroSerie: d.NumeroSerie,
                Patrimonio: d.AssetTag,
                Unidade: d.UnidadeNome,
                // Mesma regra das telas: ativo do local responde pela unidade, não por uma pessoa.
                Responsavel: d.PertenceAUsuario ? d.UsuarioNome : d.UnidadeNome,
                Status: RotuloStatus[d.Status],
                // CriadoEm é gravado em UTC (EntidadeBase), mas o SQL Server devolve Kind=Unspecified:
                // sem marcar antes de converter, a data de um cadastro da noite sai um dia adiantada.
                CadastradoEm: DateTime.SpecifyKind(d.CriadoEm, DateTimeKind.Utc).ToLocalTime(),
                // Vencimento é gravado como data pura (sem hora): não passa por conversão de fuso.
                VencimentoAluguel: d.VencimentoAluguel);
        })
        .OrderBy(l => l.Identificacao, StringComparer.CurrentCultureIgnoreCase)
        .ToList();
    }

    public async Task<ResumoRelatoriosDto> ResumoAsync(CancellationToken ct = default)
    {
        var linhas = await CarregarAsync(ct);
        var alugados = linhas.Count(l => l.Origem == ClassificacaoAluguel.Rotulo(true));

        return new ResumoRelatoriosDto(
            TotalAtivos: linhas.Count,
            Alugados: alugados,
            Proprios: linhas.Count - alugados,
            Unidades: linhas.Where(l => l.Unidade != null).Select(l => l.Unidade!).Distinct().Count(),
            PorTipo: linhas.GroupBy(l => l.Tipo)
                .Select(g => new ContagemDto(g.Key, g.Count()))
                .OrderByDescending(c => c.Quantidade).ToList(),
            PorStatus: linhas.GroupBy(l => l.Status)
                .Select(g => new ContagemDto(g.Key, g.Count()))
                .OrderByDescending(c => c.Quantidade).ToList(),
            GeradoEm: DateTime.Now);
    }

    public async Task<IReadOnlyList<LinhaInventarioDto>> InventarioAsync(bool somenteAlugados, CancellationToken ct = default)
    {
        var linhas = await CarregarAsync(ct);
        return somenteAlugados
            ? linhas.Where(l => l.Origem == ClassificacaoAluguel.Rotulo(true)).ToList()
            : linhas;
    }

    public async Task<IReadOnlyList<LinhaUnidadeDto>> PorUnidadeAsync(CancellationToken ct = default)
    {
        var linhas = await CarregarAsync(ct);

        return linhas
            .GroupBy(l => l.Unidade ?? "(sem unidade)")
            .Select(g => new LinhaUnidadeDto(
                Unidade: g.Key,
                Total: g.Count(),
                Alugados: g.Count(l => l.Origem == ClassificacaoAluguel.Rotulo(true)),
                PorStatus: g.GroupBy(l => l.Status)
                    .Select(s => new ContagemDto(s.Key, s.Count()))
                    .OrderByDescending(c => c.Quantidade).ToList()))
            .OrderByDescending(u => u.Total)
            .ThenBy(u => u.Unidade, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Computadores (notebooks) com a situação do termo de uso. Vale só o termo do detentor ATUAL:
    /// o termo de quem usou o aparelho antes não cobre quem está com ele agora. Um termo só existe
    /// depois do upload do PDF assinado (gerar o PDF não grava nada), então "com termo" é sempre
    /// documento assinado anexado. Ordem: sem termo primeiro, que é o que pede ação da TI.
    /// </summary>
    public async Task<(IReadOnlyList<LinhaTermoDto> Linhas, ResumoTermosDto Resumo)> TermosComputadoresAsync(
        CancellationToken ct = default)
    {
        var computadores = await _db.Set<Domain.Entities.Notebook>().AsNoTracking()
            .Select(n => new
            {
                n.Id,
                n.IdentificacaoInterna,
                n.Hostname,
                n.NumeroSerie,
                n.AssetTag,
                n.Status,
                n.PertenceAUsuario,
                n.UsuarioAtualId,
                UnidadeNome = n.Unidade != null ? n.Unidade.Nome : null,
                UsuarioNome = n.UsuarioAtual != null ? n.UsuarioAtual.Nome : null,
            })
            .ToListAsync(ct);

        // Filtra pelo tipo no banco (join) em vez de um Contains com milhares de ids.
        var termos = await _db.TermosDeUso.AsNoTracking()
            .Where(t => t.Dispositivo!.Tipo == TipoDispositivo.Notebook)
            .Select(t => new
            {
                t.DispositivoId,
                t.UsuarioId,
                UsuarioNome = t.Usuario != null ? t.Usuario.Nome : null,
                t.VersaoTermo,
                t.DataAceite,
            })
            .ToListAsync(ct);
        var termosPorComputador = termos
            .GroupBy(t => t.DispositivoId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(t => t.DataAceite).ToList());

        var linhas = computadores.Select(c =>
        {
            var doAparelho = termosPorComputador.GetValueOrDefault(c.Id) ?? new();
            // Ativo do local não tem detentor nominal, mesmo que sobre um UsuarioAtualId antigo.
            var detentorId = c.PertenceAUsuario ? c.UsuarioAtualId : null;
            var doDetentor = detentorId is null ? null : doAparelho.FirstOrDefault(t => t.UsuarioId == detentorId);

            var situacao = detentorId is null ? SituacaoTermo.NaoSeAplica
                : doDetentor is not null ? SituacaoTermo.ComTermo
                : SituacaoTermo.SemTermo;

            return new LinhaTermoDto(
                Identificacao: ClassificacaoAluguel.IdentificacaoExibida(c.IdentificacaoInterna, c.Hostname, c.NumeroSerie),
                NumeroSerie: c.NumeroSerie,
                Patrimonio: c.AssetTag,
                Unidade: c.UnidadeNome,
                Detentor: detentorId is null ? null : c.UsuarioNome,
                Status: RotuloStatus[c.Status],
                Situacao: situacao,
                VersaoTermo: doDetentor?.VersaoTermo,
                DataAceite: doDetentor is null ? null : FusoBrasil.ParaBrasilia(doDetentor.DataAceite),
                TermoAnteriorDe: situacao == SituacaoTermo.SemTermo
                    ? doAparelho.FirstOrDefault(t => t.UsuarioId != detentorId)?.UsuarioNome
                    : null);
        })
        .OrderBy(l => l.Situacao switch
        {
            SituacaoTermo.SemTermo => 0,
            SituacaoTermo.ComTermo => 1,
            _ => 2,
        })
        .ThenBy(l => l.Identificacao, StringComparer.CurrentCultureIgnoreCase)
        .ToList();

        var resumo = new ResumoTermosDto(
            Computadores: linhas.Count,
            ComTermo: linhas.Count(l => l.Situacao == SituacaoTermo.ComTermo),
            SemTermo: linhas.Count(l => l.Situacao == SituacaoTermo.SemTermo),
            SemDetentor: linhas.Count(l => l.Situacao == SituacaoTermo.NaoSeAplica),
            GeradoEm: FusoBrasil.ParaBrasilia(DateTime.UtcNow));

        return (linhas, resumo);
    }
}
