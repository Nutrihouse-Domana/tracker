using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Tracker.Api.Agente;
using Tracker.Api.Seguranca;
using Tracker.Application.Dtos;
using Tracker.Application.Seguranca;
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
    private readonly IAuditoria _auditoria;
    private readonly IValidator<EmitirComandoRequest> _comandoValidator;

    public DispositivosAgenteController(
        TrackerDbContext db,
        AgentTokenService tokenService,
        UserManager<UsuarioADM> userManager,
        IHubContext<ComandoRemotoHub> hub,
        IAuditoria auditoria,
        IValidator<EmitirComandoRequest> comandoValidator)
    {
        _db = db;
        _tokenService = tokenService;
        _userManager = userManager;
        _hub = hub;
        _auditoria = auditoria;
        _comandoValidator = comandoValidator;
    }

    /// <summary>Gera (ou regenera) o token do agente. Retorna o token em texto uma única vez.</summary>
    [HttpPost("{id:guid}/enrollment")]
    [Authorize(Policy = Permissoes.DispositivosCadastrar)]
    public async Task<ActionResult<EnrollmentResponse>> GerarEnrollment(Guid id)
    {
        if (await _db.Dispositivos.FirstOrDefaultAsync(d => d.Id == id) is not DispositivoComAgente dispositivo)
            return NotFound(new { message = "Dispositivo com agente (notebook/mobile) não encontrado." });

        var (token, hash) = _tokenService.Gerar();
        dispositivo.TokenAgenteHash = hash;
        _auditoria.Registrar(AcaoAuditoria.EnrollmentGerado,
            $"Gerou o token do agente de {dispositivo.NumeroSerie}.", "Dispositivo", dispositivo.Id);
        await _db.SaveChangesAsync();

        return Ok(new EnrollmentResponse(dispositivo.Id, token, dispositivo.AssetTag));
    }

    /// <summary>Emite um comando de bloqueio/desbloqueio e faz push imediato via SignalR.</summary>
    [HttpPost("{id:guid}/comandos")]
    [Authorize(Policy = Permissoes.DispositivosBloquear)]
    public async Task<ActionResult<ComandoRemotoDto>> EmitirComando(Guid id, EmitirComandoRequest request)
    {
        // Sem isto um "tipoComando": 7 seria gravado e auditado como "Desbloqueou" (o ternário
        // abaixo trata tudo que não é Bloquear como desbloqueio).
        var validation = await _comandoValidator.ValidateAsync(request);
        if (!validation.IsValid) return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));

        if (await _db.Dispositivos.FirstOrDefaultAsync(d => d.Id == id) is not DispositivoComAgente dispositivo)
            return NotFound(new { message = "Dispositivo com agente não encontrado." });

        var admId = User.AdmId()!.Value;

        var comando = new ComandoRemoto
        {
            DispositivoId = id,
            TipoComando = request.TipoComando,
            EmitidoPorAdmId = admId,
            Status = StatusComandoRemoto.Pendente,
        };
        _db.ComandosRemotos.Add(comando);
        dispositivo.StatusUltimoComando = StatusComandoRemoto.Pendente;
        _auditoria.Registrar(
            request.TipoComando == TipoComandoRemoto.Bloquear ? AcaoAuditoria.ComandoBloquear : AcaoAuditoria.ComandoDesbloquear,
            $"{(request.TipoComando == TipoComandoRemoto.Bloquear ? "Bloqueou" : "Desbloqueou")} o dispositivo {dispositivo.NumeroSerie}.",
            "Dispositivo", dispositivo.Id);
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
    [Authorize(Policy = Permissoes.DispositivosVer)]
    public async Task<ActionResult<IEnumerable<ComandoRemotoDto>>> ListarComandos(Guid id)
    {
        var comandos = await _db.ComandosRemotos.AsNoTracking()
            .Where(c => c.DispositivoId == id)
            .OrderByDescending(c => c.DataEmissao)
            .Select(c => MapToDto(c))
            .ToListAsync();

        return Ok(comandos);
    }

    /// <summary>Alertas reportados pelo agente (ex.: admin desativado no Android), recentes antes.</summary>
    [HttpGet("{id:guid}/alertas")]
    [Authorize(Policy = Permissoes.DispositivosVer)]
    public async Task<ActionResult<IEnumerable<AlertaDispositivoDto>>> ListarAlertas(Guid id)
    {
        var alertas = await _db.AlertasDispositivo.AsNoTracking()
            .Where(a => a.DispositivoId == id)
            .OrderByDescending(a => a.CriadoEm)
            .Take(100)
            .Select(a => new AlertaDispositivoDto(
                a.Id, a.DispositivoId, a.Tipo, a.Detalhe, a.Reconhecido, a.CriadoEm, a.ReconhecidoEm))
            .ToListAsync();

        return Ok(alertas);
    }

    /// <summary>Marca o alerta como tratado pela TI, tirando-o das pendências do dispositivo.</summary>
    [HttpPost("{id:guid}/alertas/{alertaId:guid}/reconhecer")]
    [Authorize(Policy = Permissoes.DispositivosBloquear)]
    public async Task<IActionResult> ReconhecerAlerta(Guid id, Guid alertaId)
    {
        var alerta = await _db.AlertasDispositivo
            .FirstOrDefaultAsync(a => a.Id == alertaId && a.DispositivoId == id);
        if (alerta is null) return NotFound();

        alerta.Reconhecido = true;
        alerta.ReconhecidoEm = DateTime.UtcNow;
        alerta.ReconhecidoPorAdmId = User.AdmId()!.Value;

        _auditoria.Registrar(AcaoAuditoria.AlertaReconhecido,
            "Reconheceu um alerta do dispositivo.", "Dispositivo", id, alerta.Tipo.ToString());
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static ComandoRemotoDto MapToDto(ComandoRemoto c) => new(
        c.Id, c.DispositivoId, c.TipoComando, c.Status, c.EmitidoPorAdmId, c.DataEmissao, c.DataConfirmacao);
}
