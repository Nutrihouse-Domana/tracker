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
using Tracker.Infrastructure.Storage;

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

        if (request.Tipo is not (TipoDispositivo.Notebook or TipoDispositivo.Mobile))
            return BadRequest(new { message = "Auto-cadastro só vale para notebook ou mobile." });

        // Arquivados ficam fora do filtro global mas seguram o índice único de NumeroSerie: sem
        // esta checagem o INSERT estouraria com erro cru de banco.
        var arquivado = await _db.Dispositivos.IgnoreQueryFilters()
            .AnyAsync(d => d.NumeroSerie == request.NumeroSerie && d.ExcluidoEm != null);
        if (arquivado)
            return Conflict(new { message = "Já existe um dispositivo arquivado com este número de série. Restaure-o no painel antes de registrar." });

        var existente = await _db.Dispositivos.FirstOrDefaultAsync(d => d.NumeroSerie == request.NumeroSerie);
        if (existente is not null && existente is not DispositivoComAgente)
            return Conflict(new { message = "Já existe um dispositivo (sem agente) com este número de série." });

        // Segunda chance pelo IMEI: o mesmo aparelho pode ter sido cadastrado antes pela TI (com o
        // serial de fábrica) e agora se registrar com o ANDROID_ID — o IMEI digitado pelo técnico
        // religa os dois em vez de duplicar o ativo.
        if (existente is null && !string.IsNullOrWhiteSpace(request.Imei))
            existente = await _db.Set<Mobile>().FirstOrDefaultAsync(m => m.Imei == request.Imei);

        var dispositivo = existente as DispositivoComAgente;
        if (dispositivo is null)
        {
            dispositivo = request.Tipo == TipoDispositivo.Mobile
                ? new Mobile { Imei = request.Imei }
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
        // IMEI informado (digitado pelo técnico ou lido com Device Owner) completa o cadastro.
        if (dispositivo is Mobile mobile && !string.IsNullOrWhiteSpace(request.Imei))
            mobile.Imei = request.Imei;
        dispositivo.Fabricante = request.Fabricante ?? dispositivo.Fabricante;
        dispositivo.Modelo = request.Modelo ?? dispositivo.Modelo;
        dispositivo.NivelProtecao = request.NivelProtecao ?? dispositivo.NivelProtecao;

        var (token, hash) = _tokenService.Gerar();
        dispositivo.TokenAgenteHash = hash;

        await _db.SaveChangesAsync();
        return Ok(new EnrollmentResponse(dispositivo.Id, token, dispositivo.AssetTag));
    }

    [HttpPost("heartbeat")]
    public async Task<ActionResult<HeartbeatResponse>> Heartbeat(HeartbeatRequest request)
    {
        var dispositivo = await AutenticarAsync();
        if (dispositivo is null) return Unauthorized();

        dispositivo.UltimoHeartbeatEm = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(request.AgenteVersao)) dispositivo.AgenteVersao = request.AgenteVersao;
        if (request.AgenteVersaoCode is { } versaoCode) dispositivo.AgenteVersaoCode = versaoCode;
        if (!string.IsNullOrWhiteSpace(request.Hostname)) dispositivo.Hostname = request.Hostname;
        if (request.BateriaPercentual is { } bateria) dispositivo.BateriaPercentual = bateria;

        // IMEI só vira disponível ao agente quando o aparelho é Device Owner — pode acontecer
        // depois do enrollment. Preenche apenas quando o cadastro está vazio: o valor digitado
        // pela TI não deve ser sobrescrito por telemetria.
        if (dispositivo is Mobile mobile &&
            string.IsNullOrWhiteSpace(mobile.Imei) &&
            !string.IsNullOrWhiteSpace(request.Imei))
        {
            mobile.Imei = request.Imei;
        }

        // Queda de proteção (usuário desativou o admin no Android) vira alerta: sem isso a TI
        // continuaria vendo o aparelho como protegido.
        if (request.NivelProtecao is { } nivel)
        {
            if (dispositivo.NivelProtecao is { } anterior && nivel < anterior)
            {
                _db.AlertasDispositivo.Add(new AlertaDispositivo
                {
                    DispositivoId = dispositivo.Id,
                    Tipo = TipoAlertaDispositivo.ProtecaoRebaixada,
                    Detalhe = $"Proteção caiu de {anterior} para {nivel}.",
                });
            }
            dispositivo.NivelProtecao = nivel;
        }

        if (request.Latitude is { } lat && request.Longitude is { } lon)
        {
            _db.HistoricoLocalizacoes.Add(new HistoricoLocalizacao
            {
                DispositivoId = dispositivo.Id,
                Latitude = lat,
                Longitude = lon,
                EnderecoAproximado = request.EnderecoAproximado,
                PrecisaoMetros = request.PrecisaoMetros,
                // O agente Windows não informa a origem; mantém o valor histórico dele.
                Origem = request.Origem ?? OrigemLocalizacao.IpGeolocationWindows,
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

        // Estado desejado: bloqueado, a menos que exista um desbloqueio esperando entrega — senão
        // o agente reaplicaria a trava no mesmo ciclo em que deveria removê-la.
        var desbloqueioPendente = pendentes.Any(c => c.TipoComando == TipoComandoRemoto.Desbloquear);
        var deveEstarBloqueado = dispositivo.Status == StatusDispositivo.Bloqueado && !desbloqueioPendente;

        // Oferta de atualização OTA: só para agentes que informam o versionCode (Android). O
        // agente Windows não envia e nunca recebe oferta de APK.
        AtualizacaoAgenteDto? atualizacao = null;
        if (request.AgenteVersaoCode is { } versaoAtual)
        {
            var vigente = await _db.PacotesAgenteAndroid.AsNoTracking()
                .OrderByDescending(p => p.VersaoCode)
                .FirstOrDefaultAsync();

            if (vigente is not null && vigente.VersaoCode > versaoAtual)
            {
                atualizacao = new AtualizacaoAgenteDto(
                    vigente.VersaoCode, vigente.VersaoNome, vigente.HashSha256, vigente.TamanhoBytes);
            }
        }

        var dto = new HeartbeatResponse(
            pendentes.Select(c => new ComandoPendenteDto(c.Id, c.TipoComando)).ToList(),
            deveEstarBloqueado,
            atualizacao);
        return Ok(dto);
    }

    /// <summary>
    /// Entrega o APK do pacote vigente ao agente (autenticado pelo token do dispositivo). O agente
    /// confere o SHA-256 contra o hash anunciado no heartbeat antes de instalar — se uma versão
    /// nova for publicada entre o anúncio e o download, o hash não bate e ele tenta de novo depois.
    /// </summary>
    [HttpGet("atualizacao/apk")]
    public async Task<IActionResult> BaixarPacoteAtualizacao(
        [FromServices] IArmazenamentoPacotesAgenteService pacotes)
    {
        var dispositivo = await AutenticarAsync();
        if (dispositivo is null) return Unauthorized();

        var vigente = await _db.PacotesAgenteAndroid.AsNoTracking()
            .OrderByDescending(p => p.VersaoCode)
            .FirstOrDefaultAsync();
        if (vigente is null) return NotFound(new { message = "Nenhum pacote publicado." });

        var stream = await pacotes.AbrirAsync(vigente.CaminhoRelativo);
        return File(stream, "application/vnd.android.package-archive", $"tracker-agent-{vigente.VersaoNome}.apk");
    }

    /// <summary>
    /// O agente reporta um evento que ele mesmo detectou (ex.: o usuário desativou a permissão de
    /// administrador no Android). É o que permite à TI saber que um aparelho ficou desprotegido.
    /// </summary>
    [HttpPost("alertas")]
    public async Task<IActionResult> RegistrarAlerta(AlertaAgenteRequest request)
    {
        var dispositivo = await AutenticarAsync();
        if (dispositivo is null) return Unauthorized();

        _db.AlertasDispositivo.Add(new AlertaDispositivo
        {
            DispositivoId = dispositivo.Id,
            Tipo = request.Tipo,
            Detalhe = request.Detalhe,
        });

        // Admin desativado significa que o agente perdeu a capacidade de bloquear: registra a
        // perda de proteção junto, para a lista de dispositivos refletir a realidade.
        if (request.Tipo == TipoAlertaDispositivo.AdminDesativado)
            dispositivo.NivelProtecao = NivelProtecaoAgente.Nenhuma;

        await _db.SaveChangesAsync();
        return NoContent();
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
