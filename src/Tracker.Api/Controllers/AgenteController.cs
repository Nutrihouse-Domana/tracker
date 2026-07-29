using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tracker.Api.Agente;
using Tracker.Application.Dtos;
using Tracker.Domain.Entities;
using Tracker.Domain.Enums;
using Tracker.Infrastructure.Data;
using Tracker.Infrastructure.Security;

namespace Tracker.Api.Controllers;

/// <summary>
/// Endpoints consumidos pelo próprio agente instalado no dispositivo.
/// Autenticação por token de dispositivo (header X-Agent-Token), não por login ADM.
/// </summary>
[ApiController]
[Route("api/agente")]
[AllowAnonymous]
public class AgenteController : ControllerBase
{
    public const string HeaderEnrollmentKey = "X-Enrollment-Key";

    private readonly TrackerDbContext _db;
    private readonly AgentAuth _agentAuth;
    private readonly AgentTokenService _tokenService;
    private readonly IConfiguration _config;

    public AgenteController(
        TrackerDbContext db, AgentAuth agentAuth, AgentTokenService tokenService, IConfiguration config)
    {
        _db = db;
        _agentAuth = agentAuth;
        _tokenService = tokenService;
        _config = config;
    }

    private Task<DispositivoComAgente?> AutenticarAsync() =>
        _agentAuth.ResolverAsync(Request.Headers[AgentAuth.HeaderToken].ToString());

    /// <summary>
    /// Auto-cadastro: cria o dispositivo (se ainda não existir pelo número de série) e emite um
    /// token individual. Protegido pela chave da empresa (Enrollment:CompanyKey).
    /// </summary>
    [HttpPost("auto-enrollment")]
    public async Task<ActionResult<EnrollmentResponse>> AutoEnrollment(AutoEnrollmentRequest request)
    {
        var chaveConfigurada = _config["Enrollment:CompanyKey"];
        var chaveRecebida = Request.Headers[HeaderEnrollmentKey].ToString();

        if (string.IsNullOrEmpty(chaveConfigurada) ||
            !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(chaveRecebida), Encoding.UTF8.GetBytes(chaveConfigurada)))
        {
            return Unauthorized(new { message = "Chave de instalação inválida." });
        }

        if (request.Tipo is not (TipoDispositivo.Notebook or TipoDispositivo.Telefone))
            return BadRequest(new { message = "Auto-cadastro só vale para notebook ou telefone." });

        var existente = await _db.Dispositivos.FirstOrDefaultAsync(d => d.NumeroSerie == request.NumeroSerie);
        if (existente is not null && existente is not DispositivoComAgente)
            return Conflict(new { message = "Já existe um dispositivo (sem agente) com este número de série." });

        var dispositivo = existente as DispositivoComAgente;
        if (dispositivo is null)
        {
            dispositivo = request.Tipo == TipoDispositivo.Telefone
                ? new Telefone { Imei = request.Imei ?? string.Empty }
                : new Notebook();
            dispositivo.NumeroSerie = request.NumeroSerie;
            // Patrimônio provisório — a TI ajusta depois no cadastro.
            dispositivo.AssetTag = $"AUTO-{request.NumeroSerie}";
            dispositivo.DataAquisicao = DateTime.UtcNow;
            dispositivo.Status = StatusDispositivo.EmEstoque;
            _db.Dispositivos.Add(dispositivo);
        }

        dispositivo.Hostname = request.Hostname;
        dispositivo.SistemaOperacional = request.SistemaOperacional;
        dispositivo.VersaoSO = request.VersaoSO;
        dispositivo.MacAddress = request.MacAddress;

        var (token, hash) = _tokenService.Gerar();
        dispositivo.TokenAgenteHash = hash;

        await _db.SaveChangesAsync();
        return Ok(new EnrollmentResponse(dispositivo.Id, token));
    }

    [HttpPost("heartbeat")]
    public async Task<ActionResult<HeartbeatResponse>> Heartbeat(HeartbeatRequest request)
    {
        var dispositivo = await AutenticarAsync();
        if (dispositivo is null) return Unauthorized();

        dispositivo.UltimoHeartbeatEm = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(request.AgenteVersao)) dispositivo.AgenteVersao = request.AgenteVersao;
        if (!string.IsNullOrWhiteSpace(request.Hostname)) dispositivo.Hostname = request.Hostname;

        if (request.Latitude is { } lat && request.Longitude is { } lon)
        {
            _db.HistoricoLocalizacoes.Add(new HistoricoLocalizacao
            {
                DispositivoId = dispositivo.Id,
                Latitude = lat,
                Longitude = lon,
                EnderecoAproximado = request.EnderecoAproximado,
                PrecisaoMetros = request.PrecisaoMetros,
                Origem = OrigemLocalizacao.IpGeolocationWindows,
            });
        }

        // Comandos ainda não confirmados são reentregues no heartbeat (rede de segurança
        // caso o push via SignalR não tenha chegado). Ao entregar, marcamos como Enviado.
        var pendentes = await _db.ComandosRemotos
            .Where(c => c.DispositivoId == dispositivo.Id &&
                        (c.Status == StatusComandoRemoto.Pendente || c.Status == StatusComandoRemoto.Enviado))
            .OrderBy(c => c.DataEmissao)
            .ToListAsync();

        foreach (var comando in pendentes.Where(c => c.Status == StatusComandoRemoto.Pendente))
            comando.Status = StatusComandoRemoto.Enviado;

        await _db.SaveChangesAsync();

        var dto = new HeartbeatResponse(
            pendentes.Select(c => new ComandoPendenteDto(c.Id, c.TipoComando)).ToList());
        return Ok(dto);
    }

    [HttpPost("comandos/{comandoId:guid}/confirmar")]
    public async Task<IActionResult> ConfirmarComando(Guid comandoId)
    {
        var dispositivo = await AutenticarAsync();
        if (dispositivo is null) return Unauthorized();

        var comando = await _db.ComandosRemotos
            .FirstOrDefaultAsync(c => c.Id == comandoId && c.DispositivoId == dispositivo.Id);
        if (comando is null) return NotFound();

        comando.Status = StatusComandoRemoto.Confirmado;
        comando.DataConfirmacao = DateTime.UtcNow;
        dispositivo.StatusUltimoComando = StatusComandoRemoto.Confirmado;

        // Reflete o efeito do comando no status do dispositivo.
        dispositivo.Status = comando.TipoComando == TipoComandoRemoto.Bloquear
            ? StatusDispositivo.Bloqueado
            : dispositivo.UsuarioAtualId is null ? StatusDispositivo.EmEstoque : StatusDispositivo.EmUso;

        await _db.SaveChangesAsync();
        return NoContent();
    }
}
