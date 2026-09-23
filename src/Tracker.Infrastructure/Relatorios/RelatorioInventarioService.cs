using Microsoft.EntityFrameworkCore;
using Tracker.Application.Relatorios;
using Tracker.Domain.Enums;
using Tracker.Infrastructure.Data;

namespace Tracker.Infrastructure.Relatorios;

public interface IRelatorioInventarioService
{
    Task<ResumoRelatoriosDto> ResumoAsync(CancellationToken ct = default);
    Task<IReadOnlyList<LinhaInventarioDto>> InventarioAsync(bool somenteAlugados, CancellationToken ct = default);
    Task<IReadOnlyList<LinhaUnidadeDto>> PorUnidadeAsync(CancellationToken ct = default);
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

            return new LinhaInventarioDto(
                Identificacao: identificacao,
                Tipo: RotuloTipo[d.Tipo],
                Origem: ClassificacaoAluguel.Rotulo(ClassificacaoAluguel.EhAlugado(identificacao)),
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
}
