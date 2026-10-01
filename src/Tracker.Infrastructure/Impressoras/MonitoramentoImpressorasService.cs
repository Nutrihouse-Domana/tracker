using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tracker.Application.Common;
using Tracker.Application.Dtos;
using Tracker.Application.Impressoras;
using Tracker.Domain.Entities;
using Tracker.Domain.Enums;
using Tracker.Infrastructure.Data;

namespace Tracker.Infrastructure.Impressoras;

public enum SituacaoConsultaImpressora
{
    Ok,
    /// <summary>Id inexistente, arquivado ou de outro tipo de dispositivo (404).</summary>
    NaoEncontrada,
    /// <summary>Monitoramento desligado, USB ou sem endereço (400).</summary>
    NaoMonitoravel,
    /// <summary>Período de/até inválido (400).</summary>
    PeriodoInvalido,
}

public sealed record ResultadoConsultaImpressora<T>(SituacaoConsultaImpressora Situacao, T? Valor, string? Mensagem = null)
{
    public static ResultadoConsultaImpressora<T> Falha(SituacaoConsultaImpressora s, string mensagem) => new(s, default, mensagem);
}

/// <summary>
/// Trava por impressora, compartilhada pelo ciclo e pelo "Ler agora": sem ela, duas leituras
/// simultâneas da mesma impressora podiam conferir "não há alerta aberto" ao mesmo tempo e
/// criar o alerta em dobro. Singleton.
/// </summary>
public class TravasLeituraImpressora
{
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _travas = new();

    public SemaphoreSlim Para(Guid impressoraId) => _travas.GetOrAdd(impressoraId, _ => new SemaphoreSlim(1, 1));
}

/// <summary>
/// Monitoramento SNMP das impressoras (scoped: um DbContext por instância). Usado pelo
/// <see cref="MonitoramentoImpressorasWorker"/> — que cria um escopo POR impressora — e pelo
/// <c>ImpressorasController</c>.
/// </summary>
public class MonitoramentoImpressorasService
{
    /// <summary>Maior período aceito em GET leituras: evita devolver anos de leituras de uma vez.</summary>
    public const int MaxDiasPeriodo = 366;

    private readonly TrackerDbContext _db;
    private readonly LeitorSnmpImpressora _leitor;
    private readonly TravasLeituraImpressora _travas;
    private readonly IOptionsMonitor<OpcoesSnmp> _opcoes;
    private readonly ILogger<MonitoramentoImpressorasService> _logger;

    public MonitoramentoImpressorasService(
        TrackerDbContext db,
        LeitorSnmpImpressora leitor,
        TravasLeituraImpressora travas,
        IOptionsMonitor<OpcoesSnmp> opcoes,
        ILogger<MonitoramentoImpressorasService> logger)
    {
        _db = db;
        _leitor = leitor;
        _travas = travas;
        _opcoes = opcoes;
        _logger = logger;
    }

    /// <summary>
    /// Ids das impressoras que entram no ciclo. Arquivadas já saem pelo filtro global; USB e sem
    /// endereço ficam de fora aqui (mesma regra de <see cref="InterpretacaoSnmpImpressora.EhMonitoravel"/>).
    /// </summary>
    public async Task<IReadOnlyList<Guid>> ListarMonitoraveisAsync(CancellationToken ct) =>
        await _db.Set<Impressora>().AsNoTracking()
            .Where(i => i.MonitorarSnmp
                && i.Conexao == ConexaoImpressora.Rede
                && i.EnderecoRede != null
                && i.EnderecoRede.Trim() != "")
            .Select(i => i.Id)
            .ToListAsync(ct);

    /// <summary>Lê a impressora pela rede, grava o estado/histórico e avalia os alertas.</summary>
    public async Task<ResultadoConsultaImpressora<MonitoramentoImpressoraDto>> LerERegistrarAsync(
        Guid impressoraId, CancellationToken ct, DateTime? agora = null)
    {
        var trava = _travas.Para(impressoraId);
        await trava.WaitAsync(ct);
        try
        {
            var impressora = await _db.Set<Impressora>().FirstOrDefaultAsync(i => i.Id == impressoraId, ct);
            if (impressora is null)
                return ResultadoConsultaImpressora<MonitoramentoImpressoraDto>.Falha(
                    SituacaoConsultaImpressora.NaoEncontrada, "Impressora não encontrada.");
            if (!InterpretacaoSnmpImpressora.EhMonitoravel(impressora))
                return ResultadoConsultaImpressora<MonitoramentoImpressoraDto>.Falha(
                    SituacaoConsultaImpressora.NaoMonitoravel, MensagemNaoMonitoravel(impressora));

            var resultado = await _leitor.LerAsync(impressora.EnderecoRede!.Trim(), ct);
            await RegistrarAsync(impressora, resultado, agora ?? DateTime.UtcNow, ct);
            return new(SituacaoConsultaImpressora.Ok, ParaDto(impressora));
        }
        finally
        {
            trava.Release();
        }
    }

    /// <summary>
    /// Grava o resultado de uma leitura: último estado na impressora, uma <see cref="LeituraImpressora"/>
    /// e os alertas — tudo num SaveChanges só. Público para os testes simularem leituras sem rede.
    /// </summary>
    public async Task RegistrarAsync(Impressora impressora, ResultadoLeituraSnmp resultado, DateTime agoraUtc, CancellationToken ct)
    {
        var opcoes = _opcoes.CurrentValue;
        var suprimentos = resultado.Dados?.Suprimentos ?? [];
        var suprimentosJson = resultado.Online ? InterpretacaoSnmpImpressora.SerializarSuprimentos(suprimentos) : null;

        impressora.UltimaLeituraEm = agoraUtc;
        impressora.Online = resultado.Online;
        if (resultado.Online)
        {
            // Offline mantém o último contador/suprimentos conhecidos (a tela mostra "última resposta há X").
            impressora.UltimaRespostaEm = agoraUtc;
            impressora.ContadorPaginas = resultado.Dados?.ContadorPaginas;
            impressora.EstadoReportado = resultado.Dados?.EstadoReportado;
            impressora.SuprimentosJson = suprimentosJson;
            impressora.ErroUltimaLeitura = null;
        }
        else
        {
            impressora.ErroUltimaLeitura = InterpretacaoSnmpImpressora.Truncar(resultado.Erro, InterpretacaoSnmpImpressora.TamanhoMaximoErro);
        }

        _db.LeiturasImpressora.Add(new LeituraImpressora
        {
            ImpressoraId = impressora.Id,
            CriadoEm = agoraUtc,
            Online = resultado.Online,
            ContadorPaginas = resultado.Online ? resultado.Dados?.ContadorPaginas : null,
            EstadoReportado = resultado.Online ? resultado.Dados?.EstadoReportado : null,
            SuprimentosJson = suprimentosJson,
            Erro = resultado.Online ? null : impressora.ErroUltimaLeitura,
        });

        await AvaliarAlertasAsync(impressora, resultado, suprimentos, opcoes, ct);
        await _db.SaveChangesAsync(ct);
    }

    private async Task AvaliarAlertasAsync(
        Impressora impressora,
        ResultadoLeituraSnmp resultado,
        IReadOnlyList<SuprimentoImpressoraDto> suprimentos,
        OpcoesSnmp opcoes,
        CancellationToken ct)
    {
        var n = Math.Max(1, opcoes.FalhasParaOffline);
        if (!resultado.Online)
        {
            // Conta pelas últimas leituras gravadas + a atual (ainda não salva).
            var anteriores = await _db.LeiturasImpressora.AsNoTracking()
                .Where(l => l.ImpressoraId == impressora.Id)
                .OrderByDescending(l => l.CriadoEm)
                .Select(l => l.Online)
                .Take(n - 1)
                .ToListAsync(ct);
            var ultimas = new List<bool> { false };
            ultimas.AddRange(anteriores);

            if (InterpretacaoSnmpImpressora.DeveAlertarOffline(ultimas, n)
                && !await TemAlertaAbertoAsync(impressora.Id, TipoAlertaDispositivo.ImpressoraOffline, ct))
            {
                var ultimaResposta = impressora.UltimaRespostaEm is { } r
                    ? FusoBrasil.ParaBrasilia(r).ToString("dd/MM/yyyy HH:mm")
                    : "nunca respondeu";
                _db.AlertasDispositivo.Add(new AlertaDispositivo
                {
                    DispositivoId = impressora.Id,
                    Tipo = TipoAlertaDispositivo.ImpressoraOffline,
                    Detalhe = InterpretacaoSnmpImpressora.Truncar(
                        $"Sem resposta SNMP em {n} leituras seguidas. Última resposta: {ultimaResposta}. {resultado.Erro}",
                        InterpretacaoSnmpImpressora.TamanhoMaximoErro),
                });
                _logger.LogWarning("Impressora {Id} ({Endereco}) sem resposta em {N} leituras seguidas.",
                    impressora.Id, impressora.EnderecoRede, n);
            }
            return;
        }

        var baixos = InterpretacaoSnmpImpressora.SuprimentosBaixos(suprimentos, opcoes.LimiteSuprimentoPercentual);
        if (baixos.Count > 0 && !await TemAlertaAbertoAsync(impressora.Id, TipoAlertaDispositivo.SuprimentoBaixo, ct))
        {
            _db.AlertasDispositivo.Add(new AlertaDispositivo
            {
                DispositivoId = impressora.Id,
                Tipo = TipoAlertaDispositivo.SuprimentoBaixo,
                Detalhe = InterpretacaoSnmpImpressora.DetalheSuprimentosBaixos(baixos),
            });
        }
    }

    private Task<bool> TemAlertaAbertoAsync(Guid impressoraId, TipoAlertaDispositivo tipo, CancellationToken ct) =>
        _db.AlertasDispositivo.AnyAsync(a => a.DispositivoId == impressoraId && a.Tipo == tipo && !a.Reconhecido, ct);

    /// <summary>Apaga as leituras mais velhas que <see cref="OpcoesSnmp.RetencaoDias"/>. Devolve quantas saíram.</summary>
    public async Task<int> PodarLeiturasAsync(DateTime agoraUtc, CancellationToken ct)
    {
        var limite = agoraUtc.AddDays(-Math.Max(1, _opcoes.CurrentValue.RetencaoDias));
        return await _db.LeiturasImpressora.Where(l => l.CriadoEm < limite).ExecuteDeleteAsync(ct);
    }

    public async Task<ResultadoConsultaImpressora<MonitoramentoImpressoraDto>> ObterMonitoramentoAsync(Guid id, CancellationToken ct)
    {
        var impressora = await _db.Set<Impressora>().AsNoTracking().FirstOrDefaultAsync(i => i.Id == id, ct);
        return impressora is null
            ? ResultadoConsultaImpressora<MonitoramentoImpressoraDto>.Falha(SituacaoConsultaImpressora.NaoEncontrada, "Impressora não encontrada.")
            : new(SituacaoConsultaImpressora.Ok, ParaDto(impressora));
    }

    /// <summary>
    /// Leituras de um período em dias civis de Brasília (<paramref name="de"/> e <paramref name="ate"/>
    /// inclusivos; padrão: do dia 1º do mês corrente até hoje) e as páginas impressas nele.
    /// </summary>
    public async Task<ResultadoConsultaImpressora<LeiturasImpressoraDto>> ObterLeiturasAsync(
        Guid id, DateOnly? de, DateOnly? ate, CancellationToken ct)
    {
        var existe = await _db.Set<Impressora>().AnyAsync(i => i.Id == id, ct);
        if (!existe)
            return ResultadoConsultaImpressora<LeiturasImpressoraDto>.Falha(SituacaoConsultaImpressora.NaoEncontrada, "Impressora não encontrada.");

        var hoje = DateOnly.FromDateTime(FusoBrasil.ParaBrasilia(DateTime.UtcNow));
        var diaFinal = ate ?? hoje;
        var diaInicial = de ?? new DateOnly(diaFinal.Year, diaFinal.Month, 1);
        if (diaInicial > diaFinal)
            return ResultadoConsultaImpressora<LeiturasImpressoraDto>.Falha(
                SituacaoConsultaImpressora.PeriodoInvalido, "A data inicial é depois da data final.");
        if (diaFinal.DayNumber - diaInicial.DayNumber + 1 > MaxDiasPeriodo)
            return ResultadoConsultaImpressora<LeiturasImpressoraDto>.Falha(
                SituacaoConsultaImpressora.PeriodoInvalido, $"Período máximo de {MaxDiasPeriodo} dias.");

        var inicioUtc = FusoBrasil.InicioDoDiaEmUtc(diaInicial);
        var fimUtc = FusoBrasil.FimDoDiaEmUtc(diaFinal); // exclusivo

        var leituras = await _db.LeiturasImpressora.AsNoTracking()
            .Where(l => l.ImpressoraId == id && l.CriadoEm >= inicioUtc && l.CriadoEm < fimUtc)
            .OrderBy(l => l.CriadoEm)
            .ToListAsync(ct);

        var contadores = leituras.Where(l => l.Online).Select(l => l.ContadorPaginas).ToList();
        var validos = contadores.Where(c => c is >= 0).Select(c => c!.Value).ToList();

        return new(SituacaoConsultaImpressora.Ok, new LeiturasImpressoraDto(
            id,
            diaInicial,
            diaFinal,
            InterpretacaoSnmpImpressora.PaginasNoPeriodo(contadores),
            validos.Count > 0 ? validos.Min() : null,
            validos.Count > 0 ? validos.Max() : null,
            leituras.Count,
            leituras.Select(l => new LeituraImpressoraDto(
                l.Id,
                DateTime.SpecifyKind(l.CriadoEm, DateTimeKind.Utc),
                l.Online,
                l.ContadorPaginas,
                l.EstadoReportado,
                InterpretacaoSnmpImpressora.DesserializarSuprimentos(l.SuprimentosJson),
                l.Erro)).ToList()));
    }

    private MonitoramentoImpressoraDto ParaDto(Impressora i) => new(
        i.Id,
        i.MonitorarSnmp,
        i.Conexao,
        i.EnderecoRede,
        InterpretacaoSnmpImpressora.EhMonitoravel(i),
        i.UltimaLeituraEm,
        i.UltimaRespostaEm,
        i.Online,
        i.ContadorPaginas,
        i.EstadoReportado,
        i.ErroUltimaLeitura,
        InterpretacaoSnmpImpressora.DesserializarSuprimentos(i.SuprimentosJson),
        _opcoes.CurrentValue.LimiteSuprimentoPercentual);

    private static string MensagemNaoMonitoravel(Impressora i)
    {
        if (i.Conexao != ConexaoImpressora.Rede)
            return "Impressora ligada por USB: o servidor não a alcança por SNMP.";
        if (string.IsNullOrWhiteSpace(i.EnderecoRede))
            return "Informe o endereço de rede (IP ou nome) da impressora para monitorá-la.";
        return "O monitoramento SNMP está desligado no cadastro desta impressora.";
    }
}
