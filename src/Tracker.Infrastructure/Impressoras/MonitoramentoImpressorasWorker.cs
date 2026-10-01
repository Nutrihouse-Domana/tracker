using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Tracker.Infrastructure.Impressoras;

/// <summary>
/// Ciclo periódico de leitura SNMP. Cada impressora é lida num ESCOPO PRÓPRIO (DbContext próprio):
/// o paralelismo nunca compartilha um DbContext, que não é thread-safe. Um erro numa impressora é
/// logado e não interrompe o ciclo. A poda do histórico roda no máximo uma vez por dia.
/// </summary>
public class MonitoramentoImpressorasWorker : BackgroundService
{
    /// <summary>Espera antes do 1º ciclo: deixa o start (migrações/seed) terminar.</summary>
    private static readonly TimeSpan AtrasoInicial = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _escopos;
    private readonly IOptionsMonitor<OpcoesSnmp> _opcoes;
    private readonly ILogger<MonitoramentoImpressorasWorker> _logger;
    private DateTime _ultimaPodaUtc = DateTime.MinValue;

    public MonitoramentoImpressorasWorker(
        IServiceScopeFactory escopos,
        IOptionsMonitor<OpcoesSnmp> opcoes,
        ILogger<MonitoramentoImpressorasWorker> logger)
    {
        _escopos = escopos;
        _opcoes = opcoes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_opcoes.CurrentValue.Habilitado)
        {
            _logger.LogInformation("Monitoramento SNMP de impressoras desligado (Snmp:Habilitado=false).");
            return;
        }

        try
        {
            await Task.Delay(AtrasoInicial, stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ExecutarCicloAsync(stoppingToken);
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogError(ex, "Falha no ciclo de monitoramento SNMP das impressoras.");
                }

                await Task.Delay(TimeSpan.FromMinutes(Math.Max(1, _opcoes.CurrentValue.IntervaloMinutos)), stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // desligamento normal
        }
    }

    /// <summary>Um ciclo completo: lista, lê em paralelo (escopo por impressora) e poda se for a hora.</summary>
    public async Task ExecutarCicloAsync(CancellationToken ct)
    {
        IReadOnlyList<Guid> ids;
        using (var escopo = _escopos.CreateScope())
        {
            ids = await escopo.ServiceProvider.GetRequiredService<MonitoramentoImpressorasService>()
                .ListarMonitoraveisAsync(ct);
        }

        var paralelismo = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Max(1, _opcoes.CurrentValue.MaxParalelismo),
            CancellationToken = ct,
        };
        var inicio = DateTime.UtcNow;

        await Parallel.ForEachAsync(ids, paralelismo, async (id, token) =>
        {
            try
            {
                using var escopo = _escopos.CreateScope();
                await escopo.ServiceProvider.GetRequiredService<MonitoramentoImpressorasService>()
                    .LerERegistrarAsync(id, token);
            }
            catch (Exception ex) when (!token.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Falha ao ler/gravar a impressora {Id}.", id);
            }
        });

        _logger.LogInformation("Ciclo SNMP: {Total} impressora(s) em {Segundos:N1}s.",
            ids.Count, (DateTime.UtcNow - inicio).TotalSeconds);

        if (DateTime.UtcNow - _ultimaPodaUtc >= TimeSpan.FromDays(1))
        {
            using var escopo = _escopos.CreateScope();
            var removidas = await escopo.ServiceProvider.GetRequiredService<MonitoramentoImpressorasService>()
                .PodarLeiturasAsync(DateTime.UtcNow, ct);
            _ultimaPodaUtc = DateTime.UtcNow;
            if (removidas > 0)
                _logger.LogInformation("Poda SNMP: {Removidas} leitura(s) fora da retenção removida(s).", removidas);
        }
    }
}
