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

    /// <summary>
    /// Serializa a mudança do sinal de bloqueio entre o hub e o heartbeat, e protege
    /// <see cref="_geracaoComandos"/>.
    /// </summary>
    private readonly object _travaBloqueio = new();

    /// <summary>
    /// Incrementado a cada comando aplicado. O heartbeat lê o valor ANTES de sair: se mudou quando a
    /// resposta chega, um comando (do hub, em paralelo) é mais novo que o <c>deveEstarBloqueado</c>
    /// daquela resposta e prevalece. Sem isso, um Desbloquear do hub seria desfeito pela resposta de
    /// um heartbeat que saiu antes dele. Mesma regra do agente Android (<c>geracaoComandos</c>).
    /// </summary>
    private long _geracaoComandos;

    public ExecutorDeComandos(BackendClient backend, IOptions<AgenteOptions> options, ILogger<ExecutorDeComandos> logger)
    {
        _backend = backend;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>Geração atual dos comandos aplicados; o heartbeat a lê antes de sair.</summary>
    public long GeracaoComandos
    {
        get { lock (_travaBloqueio) return _geracaoComandos; }
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
            lock (_travaBloqueio)
            {
                if (comando.TipoComando == "Bloquear")
                {
                    if (_options.Simular)
                        _logger.LogWarning("[SIMULACAO] Bloqueio solicitado — a tela NAO foi travada (Simular=true).");
                    else
                        BloqueioTela.Travar(_logger);
                    _geracaoComandos++;
                }
                else if (comando.TipoComando == "Desbloquear")
                {
                    if (_options.Simular)
                        _logger.LogWarning("[SIMULACAO] Desbloqueio solicitado (Simular=true).");
                    else
                        BloqueioTela.Destravar(_logger);
                    _geracaoComandos++;
                }
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

    /// <summary>
    /// Alinha o sinal local ao estado que o servidor considera correto (<c>deveEstarBloqueado</c>
    /// do heartbeat). Só age quando divergem — reaplicar a cada ciclo geraria alertas repetidos.
    /// Recupera o bloqueio se o <c>locked.flag</c> sumiu (limpeza, restauração, alguém com acesso
    /// de administrador) e o reporta como <c>BloqueioReaplicado</c>; se o servidor já não o
    /// considera bloqueado, remove a trava (a TI liberou). Resposta mais velha que o último comando
    /// aplicado (<paramref name="geracaoNoInicio"/> diferente) é descartada.
    /// </summary>
    public async Task SincronizarBloqueioAsync(bool deveEstarBloqueado, long geracaoNoInicio, CancellationToken ct)
    {
        bool reaplicado;
        lock (_travaBloqueio)
        {
            if (_geracaoComandos != geracaoNoInicio)
            {
                _logger.LogInformation("Heartbeat anterior ao último comando aplicado; estado do servidor ignorado.");
                return;
            }

            var bloqueadoLocal = File.Exists(Caminhos.FlagBloqueio);
            if (deveEstarBloqueado == bloqueadoLocal) return;

            if (_options.Simular)
            {
                _logger.LogWarning("[SIMULACAO] Servidor indica bloqueado={Deve}, sinal local={Local}; nada alterado (Simular=true).",
                    deveEstarBloqueado, bloqueadoLocal);
                return;
            }

            if (deveEstarBloqueado)
            {
                _logger.LogWarning("Servidor indica bloqueio ativo mas o sinal local se perdeu; reaplicando.");
                reaplicado = BloqueioTela.Travar(_logger);
            }
            else
            {
                _logger.LogInformation("Servidor não considera o aparelho bloqueado; removendo a trava local.");
                BloqueioTela.Destravar(_logger);
                reaplicado = false;
            }
        }

        if (!reaplicado) return;
        try
        {
            await _backend.ReportarAlertaAsync("BloqueioReaplicado",
                "Bloqueio reaplicado a partir do estado do servidor (sinal local ausente).", ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not TokenInvalidoException)
        {
            // O bloqueio já foi reaplicado; perder o alerta não é motivo para falhar o heartbeat.
            _logger.LogWarning(ex, "Não foi possível reportar o alerta de bloqueio reaplicado.");
        }
    }
}
