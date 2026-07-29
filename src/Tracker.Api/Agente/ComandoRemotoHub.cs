using Microsoft.AspNetCore.SignalR;

namespace Tracker.Api.Agente;

/// <summary>
/// Canal em tempo real para entrega de comandos (bloquear/desbloquear) aos agentes.
/// O agente conecta em /hubs/comandos?token=SEU_TOKEN; validamos o token e o colocamos
/// no grupo "device-{id}". O backend envia o evento "ExecutarComando" para esse grupo.
/// </summary>
public class ComandoRemotoHub : Hub
{
    public const string EventoExecutarComando = "ExecutarComando";

    public static string GrupoDoDispositivo(Guid dispositivoId) => $"device-{dispositivoId}";

    private readonly AgentAuth _agentAuth;

    public ComandoRemotoHub(AgentAuth agentAuth)
    {
        _agentAuth = agentAuth;
    }

    public override async Task OnConnectedAsync()
    {
        var token = Context.GetHttpContext()?.Request.Query["token"].ToString();
        var dispositivo = await _agentAuth.ResolverAsync(token);

        if (dispositivo is null)
        {
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GrupoDoDispositivo(dispositivo.Id));
        await base.OnConnectedAsync();
    }
}
