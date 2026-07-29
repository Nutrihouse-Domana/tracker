using Microsoft.Extensions.Options;

namespace Tracker.Agent.Windows;

/// <summary>
/// Executa os comandos recebidos do backend. O bloqueio trava a tela (ver BloqueioTela).
/// Limitação conhecida: o próprio usuário ainda destrava com a senha dele — um bloqueio que
/// só a TI destrava exigiria um credential provider customizado.
/// </summary>
public class ExecutorDeComandos
{
    private readonly BackendClient _backend;
    private readonly AgenteOptions _options;
    private readonly ILogger<ExecutorDeComandos> _logger;
    private readonly HashSet<Guid> _jaProcessados = new();

    public ExecutorDeComandos(BackendClient backend, IOptions<AgenteOptions> options, ILogger<ExecutorDeComandos> logger)
    {
        _backend = backend;
        _options = options.Value;
        _logger = logger;
    }

    public async Task ProcessarAsync(ComandoPendente comando, CancellationToken ct)
    {
        // Evita reexecutar o mesmo comando entregue tanto por SignalR quanto por heartbeat.
        lock (_jaProcessados)
        {
            if (!_jaProcessados.Add(comando.Id)) return;
        }

        _logger.LogInformation("Executando comando {Tipo} ({Id})", comando.TipoComando, comando.Id);

        try
        {
            if (comando.TipoComando == "Bloquear")
            {
                if (_options.Simular)
                    _logger.LogWarning("[SIMULACAO] Bloqueio solicitado — a tela NAO foi travada (Simular=true).");
                else
                    BloqueioTela.Travar(_logger);
            }
            else if (comando.TipoComando == "Desbloquear")
            {
                if (_options.Simular)
                    _logger.LogWarning("[SIMULACAO] Desbloqueio solicitado (Simular=true).");
                else
                    BloqueioTela.Destravar(_logger);
            }

            await _backend.ConfirmarComandoAsync(comando.Id, ct);
            _logger.LogInformation("Comando {Id} confirmado ao backend.", comando.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao processar/confirmar comando {Id}.", comando.Id);
            lock (_jaProcessados) { _jaProcessados.Remove(comando.Id); } // permite retry no próximo heartbeat
        }
    }
}
