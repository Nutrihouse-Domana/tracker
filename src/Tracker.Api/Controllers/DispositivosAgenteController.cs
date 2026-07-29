using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Tracker.Api.Agente;
using Tracker.Application.Dtos;
using Tracker.Domain.Entities;
using Tracker.Domain.Enums;
using Tracker.Infrastructure.Data;
using Tracker.Infrastructure.Identity;
using Tracker.Infrastructure.Security;

namespace Tracker.Api.Controllers;

/// <summary>
/// Endpoints ADM para gerenciar o agente de um dispositivo: enrollment (gerar token),
/// emissão de comandos de bloqueio/desbloqueio e histórico. Rota compartilhada com
/// DispositivosController via prefixo, mantendo /api/dispositivos/{id}/...
/// </summary>
[ApiController]
[Route("api/dispositivos")]
[Authorize]
public class DispositivosAgenteController : ControllerBase
{
    private readonly TrackerDbContext _db;
    private readonly AgentTokenService _tokenService;
    private readonly UserManager<UsuarioADM> _userManager;
    private readonly IHubContext<ComandoRemotoHub> _hub;

    public DispositivosAgenteController(
        TrackerDbContext db,
        AgentTokenService tokenService,
        UserManager<UsuarioADM> userManager,
        IHubContext<ComandoRemotoHub> hub)
    {
        _db = db;
        _tokenService = tokenService;
        _userManager = userManager;
        _hub = hub;
    }

    /// <summary>Gera (ou regenera) o token do agente. Retorna o token em texto uma única vez.</summary>
    [HttpPost("{id:guid}/enrollment")]
    public async Task<ActionResult<EnrollmentResponse>> GerarEnrollment(Guid id)
    {
        if (await _db.Dispositivos.FirstOrDefaultAsync(d => d.Id == id) is not DispositivoComAgente dispositivo)
            return NotFound(new { message = "Dispositivo com agente (notebook/telefone) não encontrado." });

        var (token, hash) = _tokenService.Gerar();
        dispositivo.TokenAgenteHash = hash;
        await _db.SaveChangesAsync();

        return Ok(new EnrollmentResponse(dispositivo.Id, token));
    }

    /// <summary>Emite um comando de bloqueio/desbloqueio e faz push imediato via SignalR.</summary>
    [HttpPost("{id:guid}/comandos")]
    public async Task<ActionResult<ComandoRemotoDto>> EmitirComando(Guid id, EmitirComandoRequest request)
    {
        if (await _db.Dispositivos.FirstOrDefaultAsync(d => d.Id == id) is not DispositivoComAgente dispositivo)
            return NotFound(new { message = "Dispositivo com agente não encontrado." });

        var admId = Guid.Parse(_userManager.GetUserId(User)!);

        var comando = new ComandoRemoto
        {
            DispositivoId = id,
            TipoComando = request.TipoComando,
            EmitidoPorAdmId = admId,
            Status = StatusComandoRemoto.Pendente,
        };
        _db.ComandosRemotos.Add(comando);
        dispositivo.StatusUltimoComando = StatusComandoRemoto.Pendente;
        await _db.SaveChangesAsync();

        // Push em tempo real para o agente (se estiver conectado). O heartbeat é a rede de
        // segurança caso o agente esteja offline no momento.
        await _hub.Clients
            .Group(ComandoRemotoHub.GrupoDoDispositivo(id))
            .SendAsync(ComandoRemotoHub.EventoExecutarComando,
                new ComandoPendenteDto(comando.Id, comando.TipoComando));

        return Ok(MapToDto(comando));
    }

    [HttpGet("{id:guid}/comandos")]
    public async Task<ActionResult<IEnumerable<ComandoRemotoDto>>> ListarComandos(Guid id)
    {
        var comandos = await _db.ComandosRemotos.AsNoTracking()
            .Where(c => c.DispositivoId == id)
            .OrderByDescending(c => c.DataEmissao)
            .Select(c => MapToDto(c))
            .ToListAsync();

        return Ok(comandos);
    }

    private static ComandoRemotoDto MapToDto(ComandoRemoto c) => new(
        c.Id, c.DispositivoId, c.TipoComando, c.Status, c.EmitidoPorAdmId, c.DataEmissao, c.DataConfirmacao);
}
