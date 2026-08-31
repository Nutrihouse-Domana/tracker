using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;

namespace Tracker.Agent.Windows;

/// <summary>
/// Serviço principal do agente: mantém a conexão SignalR (para comandos em tempo real) e um
/// loop de heartbeat (telemetria + rede de segurança que reentrega comandos pendentes).
/// </summary>
public class Worker : BackgroundService
{
    private const string Versao = "1.0.0";

    private readonly ILogger<Worker> _logger;
    private readonly AgenteOptions _options;
    private readonly Geolocalizacao _geo;
    private readonly BackendClient _backend;
    private readonly ExecutorDeComandos _executor;
    private readonly EnrollmentService _enrollment;
    private readonly TokenHolder _tokenHolder;
    private HubConnection? _hub;
    private System.Threading.Timer? _timerBloqueio;
    private int _verificandoBloqueio;

    public Worker(
        ILogger<Worker> logger,
        IOptions<AgenteOptions> options,
        Geolocalizacao geo,
        BackendClient backend,
        ExecutorDeComandos executor,
        EnrollmentService enrollment,
        TokenHolder tokenHolder)
    {
        _logger = logger;
        _options = options.Value;
        _geo = geo;
        _backend = backend;
        _executor = executor;
        _enrollment = enrollment;
        _tokenHolder = tokenHolder;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Enforcement do bloqueio numa cadência PRÓPRIA (2s), independente de rede/heartbeat:
        // mantém a trava ativa após reiniciar, offline, e reaplica rapidamente novos bloqueios.
        if (!_options.Simular)
            _timerBloqueio = new System.Threading.Timer(_ => VerificarBloqueio(), null, TimeSpan.Zero, TimeSpan.FromSeconds(2));

        // Resolve o token (salvo, manual ou auto-cadastro) antes de qualquer comunicação.
        // Tenta em loop: no boot a rede pode ainda não estar disponível, então não desistimos.
        while (!stoppingToken.IsCancellationRequested && !await _enrollment.GarantirTokenAsync(stoppingToken))
        {
            _logger.LogWarning("Sem token ainda (backend inacessível?). Nova tentativa em 30s.");
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
        if (stoppingToken.IsCancellationRequested) return;

        await ConectarHubAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var loc = await _geo.ObterAsync(stoppingToken);
                var resp = await _backend.EnviarHeartbeatAsync(loc, Versao, stoppingToken);

                if (resp?.ComandosPendentes is { Count: > 0 } pendentes)
                {
                    _logger.LogInformation("{N} comando(s) pendente(s) no heartbeat.", pendentes.Count);
                    foreach (var c in pendentes)
                        await _executor.ProcessarAsync(c, stoppingToken);
                }
            }
            catch (OperationCanceledException) { break; }
            catch (TokenInvalidoException)
            {
                // Token rejeitado (agente atualizado ou apontado para outro servidor): re-cadastra
                // sozinho. O backend reaproveita o registro pela série/patrimônio (sem duplicar) e
                // devolve um token novo; reconecta o hub, que carrega o token na URL.
                _logger.LogWarning("Heartbeat 401 — token inválido. Re-cadastrando o agente...");
                if (await _enrollment.ReenrollarAsync(stoppingToken))
                {
                    _logger.LogInformation("Re-cadastro concluído. Reconectando ao hub.");
                    if (_hub is not null) await _hub.DisposeAsync();
                    await ConectarHubAsync(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha no heartbeat — nova tentativa no próximo ciclo.");
            }

            await Task.Delay(TimeSpan.FromSeconds(_options.HeartbeatIntervalSeconds), stoppingToken);
        }
    }

    private void VerificarBloqueio()
    {
        // Evita execuções sobrepostas (o lançamento da tela pode levar um instante).
        if (Interlocked.Exchange(ref _verificandoBloqueio, 1) == 1) return;
        try { BloqueioTela.GarantirBloqueioAtivo(_logger); }
        catch (Exception ex) { _logger.LogWarning(ex, "Falha ao verificar/aplicar bloqueio."); }
        finally { Interlocked.Exchange(ref _verificandoBloqueio, 0); }
    }

    private async Task ConectarHubAsync(CancellationToken ct)
    {
        try
        {
            var url = $"{_options.BackendBaseUrl.TrimEnd('/')}/hubs/comandos?token={Uri.EscapeDataString(_tokenHolder.Token ?? string.Empty)}";
            _hub = new HubConnectionBuilder().WithUrl(url).WithAutomaticReconnect().Build();

            _hub.On<ComandoPendente>("ExecutarComando", async (comando) =>
            {
                _logger.LogInformation("Comando recebido via SignalR: {Tipo}", comando.TipoComando);
                await _executor.ProcessarAsync(comando, ct);
            });

            await _hub.StartAsync(ct);
            _logger.LogInformation("Conectado ao hub de comandos.");
        }
        catch (Exception ex)
        {
            // Não é fatal: o heartbeat continua entregando comandos como fallback.
            _logger.LogWarning(ex, "Não foi possível conectar ao SignalR; usando apenas heartbeat.");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_timerBloqueio is not null) await _timerBloqueio.DisposeAsync();
        if (_hub is not null) await _hub.DisposeAsync();
        await base.StopAsync(cancellationToken);
    }
}
