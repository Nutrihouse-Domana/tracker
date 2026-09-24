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

    /// <summary>Intervalo de conferência do hub quando está tudo bem (troca de token, queda).</summary>
    private static readonly TimeSpan ConferenciaHub = TimeSpan.FromSeconds(10);

    private readonly ILogger<Worker> _logger;
    private readonly AgenteOptions _options;
    private readonly Geolocalizacao _geo;
    private readonly BackendClient _backend;
    private readonly ExecutorDeComandos _executor;
    private readonly EnrollmentService _enrollment;
    private readonly TokenHolder _tokenHolder;

    // A conexão do hub só é criada, iniciada e descartada pelo LacoConexaoHubAsync (uma única
    // tarefa): é isso que impede duas conexões simultâneas. Os demais só o "acordam".
    private HubConnection? _hub;
    private string? _tokenDoHub;
    private readonly SemaphoreSlim _acordarLacoHub = new(0, 1);

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
        try
        {
            while (!stoppingToken.IsCancellationRequested && !await _enrollment.GarantirTokenAsync(stoppingToken))
            {
                _logger.LogWarning("Sem token ainda (backend inacessível?). Nova tentativa em 30s.");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
        catch (OperationCanceledException) { return; }
        if (stoppingToken.IsCancellationRequested) return;

        var lacoHub = LacoConexaoHubAsync(stoppingToken);
        try
        {
            await LacoHeartbeatAsync(stoppingToken);
        }
        finally
        {
            // Aguarda o laço do hub sair antes de o StopAsync descartar a conexão.
            try { await lacoHub; } catch (OperationCanceledException) { }
        }
    }

    private async Task LacoHeartbeatAsync(CancellationToken stoppingToken)
    {
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
                // devolve um token novo. O laço do hub percebe a troca de token e reconecta com o
                // novo (o token vai na URL do hub, então a conexão antiga não serve mais).
                _logger.LogWarning("Heartbeat 401 — token inválido. Re-cadastrando o agente...");
                if (await _enrollment.ReenrollarAsync(stoppingToken))
                {
                    _logger.LogInformation("Re-cadastro concluído. Reconectando ao hub com o token novo.");
                    AcordarLacoHub();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha no heartbeat — nova tentativa no próximo ciclo.");
            }

            try { await Task.Delay(TimeSpan.FromSeconds(_options.HeartbeatIntervalSeconds), stoppingToken); }
            catch (OperationCanceledException) { break; }
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

    /// <summary>
    /// Mantém o hub conectado para sempre. O <c>WithAutomaticReconnect()</c> padrão desiste
    /// depois de 4 tentativas e não cobre a falha do PRIMEIRO <c>StartAsync</c> (backend fora no
    /// boot) — nos dois casos o agente ficava só no heartbeat até reiniciar o serviço. Aqui:
    ///   - a política <see cref="RecuoHub"/> faz a reconexão automática nunca desistir;
    ///   - este laço cobre o que ela não cobre: estado <c>Disconnected</c> (1º start falhou,
    ///     <c>Closed</c>) → novo <c>StartAsync</c> com recuo progressivo até 60s;
    ///   - troca de token (re-cadastro) → descarta a conexão e cria outra com o token novo.
    /// Só esta tarefa mexe em <see cref="_hub"/>, então não há conexões duplicadas.
    /// </summary>
    private async Task LacoConexaoHubAsync(CancellationToken ct)
    {
        var falhasSeguidas = 0;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var token = _tokenHolder.Token;

                if (_hub is not null && _tokenDoHub != token)
                {
                    _logger.LogInformation("Token do agente mudou: recriando a conexão do hub.");
                    await DescartarHubAsync();
                }

                if (_hub is null && !string.IsNullOrEmpty(token))
                {
                    _hub = CriarHub(token, ct);
                    _tokenDoHub = token;
                }

                if (_hub is { State: HubConnectionState.Disconnected })
                {
                    await _hub.StartAsync(ct);
                    _logger.LogInformation("Conectado ao hub de comandos.");
                }

                falhasSeguidas = 0;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                falhasSeguidas++;
                var espera = RecuoHub.Espera(falhasSeguidas);
                // Não é fatal: o heartbeat continua entregando comandos como fallback.
                _logger.LogWarning("Hub de comandos indisponível ({Tentativa}ª falha seguida): {Erro}. " +
                                   "Nova tentativa em {Segundos}s; enquanto isso, só heartbeat.",
                    falhasSeguidas, ex.Message, (int)espera.TotalSeconds);
            }

            var intervalo = falhasSeguidas == 0 ? ConferenciaHub : RecuoHub.Espera(falhasSeguidas);
            try { await _acordarLacoHub.WaitAsync(intervalo, ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    private HubConnection CriarHub(string token, CancellationToken ct)
    {
        var url = $"{_options.BackendBaseUrl.TrimEnd('/')}/hubs/comandos?token={Uri.EscapeDataString(token)}";
        var hub = new HubConnectionBuilder()
            .WithUrl(url)
            .WithAutomaticReconnect(new RecuoHub())
            .Build();

        hub.On<ComandoPendente>("ExecutarComando", async (comando) =>
        {
            _logger.LogInformation("Comando recebido via SignalR: {Tipo}", comando.TipoComando);
            await _executor.ProcessarAsync(comando, ct);
        });

        hub.Reconnecting += ex =>
        {
            _logger.LogWarning("Conexão com o hub caiu ({Erro}); reconectando...", ex?.Message ?? "sem detalhe");
            return Task.CompletedTask;
        };
        hub.Reconnected += _ =>
        {
            _logger.LogInformation("Reconectado ao hub de comandos.");
            return Task.CompletedTask;
        };
        hub.Closed += ex =>
        {
            // Com a política infinita isto só ocorre em parada explícita ou falha que a
            // reconexão automática não trata; o laço reabre a conexão.
            if (!ct.IsCancellationRequested)
            {
                _logger.LogWarning("Conexão com o hub fechada ({Erro}); o laço vai reabrir.", ex?.Message ?? "sem detalhe");
                AcordarLacoHub();
            }
            return Task.CompletedTask;
        };

        return hub;
    }

    private async Task DescartarHubAsync()
    {
        var antigo = _hub;
        _hub = null;
        _tokenDoHub = null;
        if (antigo is null) return;
        try { await antigo.DisposeAsync(); }
        catch (Exception ex) { _logger.LogWarning(ex, "Falha ao descartar a conexão antiga do hub."); }
    }

    /// <summary>Faz o laço do hub agir já, sem esperar o próximo intervalo. Idempotente.</summary>
    private void AcordarLacoHub()
    {
        try { if (_acordarLacoHub.CurrentCount == 0) _acordarLacoHub.Release(); }
        catch (SemaphoreFullException) { /* já havia um aviso pendente */ }
        catch (ObjectDisposedException) { }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_timerBloqueio is not null) await _timerBloqueio.DisposeAsync();
        // base.StopAsync cancela o stoppingToken e espera o ExecuteAsync (e o laço do hub) sair;
        // só então a conexão é descartada — sem corrida com um StartAsync em andamento.
        await base.StopAsync(cancellationToken);
        await DescartarHubAsync();
    }
}

/// <summary>
/// Recuo progressivo e SEM fim para o hub: 0s, 2s, 5s, 10s, 30s e depois 60s para sempre.
/// Usado pela reconexão automática do SignalR (que por padrão desiste após 4 tentativas) e
/// pelo laço de <c>StartAsync</c> do <see cref="Worker"/>.
/// </summary>
public sealed class RecuoHub : IRetryPolicy
{
    private static readonly TimeSpan[] Degraus =
    [
        TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(60),
    ];

    /// <summary>Nunca devolve null: devolver null é o que faz o SignalR desistir.</summary>
    public TimeSpan? NextRetryDelay(RetryContext retryContext) =>
        Degraus[(int)Math.Min(retryContext.PreviousRetryCount, Degraus.Length - 1)];

    /// <summary>Espera antes da próxima tentativa, dado o número de falhas seguidas (1 = primeira).
    /// Começa em 2s para não martelar o servidor logo após uma falha.</summary>
    public static TimeSpan Espera(int falhasSeguidas) =>
        Degraus[Math.Clamp(falhasSeguidas, 1, Degraus.Length - 1)];
}
