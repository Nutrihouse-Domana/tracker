using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tracker.Api.Agente;
using Tracker.Application.Common;
using Tracker.Application.Dispositivos;
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
    private readonly IValidator<AutoEnrollmentRequest> _enrollmentValidator;
    private readonly IValidator<AlertaAgenteRequest> _alertaValidator;

    public AgenteController(
        TrackerDbContext db,
        AgentAuth agentAuth,
        AgentTokenService tokenService,
        IConfiguration config,
        IValidator<AutoEnrollmentRequest> enrollmentValidator,
        IValidator<AlertaAgenteRequest> alertaValidator)
    {
        _db = db;
        _agentAuth = agentAuth;
        _tokenService = tokenService;
        _config = config;
        _enrollmentValidator = enrollmentValidator;
        _alertaValidator = alertaValidator;
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

        var validation = await _enrollmentValidator.ValidateAsync(request);
        if (!validation.IsValid) return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));

        if (request.Tipo is not (TipoDispositivo.Notebook or TipoDispositivo.Mobile))
            return BadRequest(new { message = "Auto-cadastro só vale para notebook ou mobile." });

        // Serial genérico de BIOS ("To be filled by O.E.M.", "0"...) é o MESMO em várias máquinas.
        // Usá-lo para reencontrar faria o segundo aparelho assumir o cadastro (e o token) do
        // primeiro. O validador só o deixa passar quando há patrimônio; nesse caso o serial
        // recebido é descartado: não entra no reencontro nem na promoção, e o cadastro novo grava
        // um serial derivado do patrimônio (único como ele), já que NumeroSerie é obrigatório e
        // tem índice único.
        var serieConfiavel = !SerialGenerico.EhGenerico(request.NumeroSerie);
        var serieEfetiva = serieConfiavel
            // Sem Trim de propósito: os cadastros existentes guardam o serial como veio do agente,
            // e mudar a forma agora faria o reencontro falhar e duplicar o ativo.
            ? request.NumeroSerie
            : $"{PrefixoSerieDerivada}{NormalizarPatrimonio(request.AssetTag)}";

        var existente = await ReencontrarAsync(request, serieConfiavel ? serieEfetiva : null);
        if (existente is not null && existente is not DispositivoComAgente)
            return Conflict(new { message = "Já existe um dispositivo (sem agente) com esta identificação." });

        var dispositivo = existente as DispositivoComAgente;
        if (dispositivo is null)
        {
            // Arquivados ficam fora do filtro global mas seguram os índices únicos de NumeroSerie
            // e AssetTag: sem esta checagem o INSERT estouraria com erro cru de banco.
            var arquivado = await _db.Dispositivos.IgnoreQueryFilters().FirstOrDefaultAsync(d =>
                d.ExcluidoEm != null &&
                (d.NumeroSerie == serieEfetiva ||
                    (request.AssetTag != null && d.AssetTag == request.AssetTag)));
            if (arquivado is not null)
            {
                return Conflict(new
                {
                    message = $"O dispositivo '{arquivado.AssetTag ?? arquivado.NumeroSerie}' está " +
                              "arquivado. Restaure-o no painel antes de registrar o aparelho.",
                });
            }

            dispositivo = request.Tipo == TipoDispositivo.Mobile
                ? new Mobile { Imei = request.Imei }
                : new Notebook();
            dispositivo.NumeroSerie = serieEfetiva;
            // Patrimônio digitado pelo técnico quando houver; senão um provisório que a TI ajusta.
            dispositivo.AssetTag = NormalizarPatrimonio(request.AssetTag) ?? $"AUTO-{serieEfetiva}";
            dispositivo.DataAquisicao = DateTime.UtcNow;
            dispositivo.Status = StatusDispositivo.EmEstoque;
            _db.Dispositivos.Add(dispositivo);
        }
        else
        {
            // Reencontrado: o aparelho é o mesmo ativo, então o registro é reaproveitado (com o
            // histórico) em vez de duplicado. Só promovemos os identificadores para valores
            // melhores — nunca rebaixamos um dado real para um provisório.
            // Serial genérico nunca substitui o que está cadastrado (nem vira o derivado).
            if (serieConfiavel) await PromoverNumeroSerieAsync(dispositivo, serieEfetiva);
            await PromoverPatrimonioAsync(dispositivo, request.AssetTag);
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

    /// <summary>
    /// Prefixo do identificador que o agente Android usa quando o sistema não entrega o serial de
    /// fábrica (todo aparelho sem Device Owner). É derivado do ANDROID_ID e, portanto, **muda
    /// quando o APK é reinstalado com outra assinatura** — foi o que já duplicou ativo aqui.
    /// </summary>
    private const string PrefixoSerieProvisoria = "ANDROID-";

    /// <summary>
    /// Prefixo do número de série gravado quando o aparelho reporta um serial genérico de BIOS e o
    /// técnico informou o patrimônio: <c>SEM-SERIE-&lt;patrimônio&gt;</c>. Único porque o
    /// patrimônio é único; nunca é usado para promover nem substituir outro serial.
    /// </summary>
    private const string PrefixoSerieDerivada = "SEM-SERIE-";

    private static bool EhSerieProvisoria(string? serie) =>
        serie is not null && serie.StartsWith(PrefixoSerieProvisoria, StringComparison.Ordinal);

    private static bool EhPatrimonioProvisorio(string? patrimonio) =>
        patrimonio is not null && patrimonio.StartsWith("AUTO-", StringComparison.Ordinal);

    private static string? NormalizarPatrimonio(string? patrimonio) =>
        string.IsNullOrWhiteSpace(patrimonio) ? null : patrimonio.Trim();

    /// <summary>
    /// Reencontra o ativo já cadastrado, na ordem em que os identificadores merecem confiança.
    /// Sem isso, um aparelho que se registra de novo entra como dispositivo novo e o inventário
    /// fica com o mesmo tablet duas vezes.
    /// </summary>
    /// <param name="request">Pedido de auto-cadastro.</param>
    /// <param name="serieConfiavel">Serial recebido, ou nulo quando é genérico de BIOS — aí ele não serve para reencontrar.</param>
    private async Task<Dispositivo?> ReencontrarAsync(AutoEnrollmentRequest request, string? serieConfiavel)
    {
        // 1) Patrimônio: a identidade corporativa do ativo, digitada pelo técnico a partir da
        //    etiqueta. É o único identificador que sobrevive a reinstalar o app com outra chave
        //    de assinatura, a limpar os dados e à troca do agente.
        var patrimonio = NormalizarPatrimonio(request.AssetTag);
        if (patrimonio is not null)
        {
            var porPatrimonio = await _db.Dispositivos.FirstOrDefaultAsync(d => d.AssetTag == patrimonio);
            if (porPatrimonio is not null) return porPatrimonio;
        }

        // 2) Número de série: o de fábrica quando o aparelho o entrega; senão o provisório, que
        //    ainda reencontra o aparelho enquanto a assinatura do APK não mudar.
        //    Serial genérico de BIOS é pulado: casaria com a máquina errada.
        if (serieConfiavel is not null)
        {
            var porSerie = await _db.Dispositivos.FirstOrDefaultAsync(d => d.NumeroSerie == serieConfiavel);
            if (porSerie is not null) return porSerie;
        }

        // 3) IMEI: só existe em aparelho com modem, e só é legível com Device Owner.
        if (!string.IsNullOrWhiteSpace(request.Imei))
            return await _db.Set<Mobile>().FirstOrDefaultAsync(m => m.Imei == request.Imei);

        return null;
    }

    /// <summary>
    /// Atualiza o número de série do ativo reencontrado, sem nunca trocar um serial de fábrica
    /// pelo provisório do app — o provisório muda a cada reinstalação com assinatura diferente.
    /// </summary>
    private async Task PromoverNumeroSerieAsync(DispositivoComAgente dispositivo, string serieRecebida)
    {
        if (string.IsNullOrWhiteSpace(serieRecebida) || dispositivo.NumeroSerie == serieRecebida) return;
        if (!EhSerieProvisoria(dispositivo.NumeroSerie) && EhSerieProvisoria(serieRecebida)) return;

        // O índice único não conhece o filtro global: um arquivado com o mesmo serial faria o
        // UPDATE estourar com erro cru de banco.
        var emUso = await _db.Dispositivos.IgnoreQueryFilters()
            .AnyAsync(d => d.NumeroSerie == serieRecebida && d.Id != dispositivo.Id);
        if (emUso) return;

        dispositivo.NumeroSerie = serieRecebida;
    }

    /// <summary>
    /// Troca o patrimônio provisório (AUTO-*) pelo que o técnico digitou. Um patrimônio já
    /// definido pela TI nunca é sobrescrito pelo agente.
    /// </summary>
    private async Task PromoverPatrimonioAsync(DispositivoComAgente dispositivo, string? patrimonioRecebido)
    {
        var patrimonio = NormalizarPatrimonio(patrimonioRecebido);
        if (patrimonio is null || dispositivo.AssetTag == patrimonio) return;
        if (!EhPatrimonioProvisorio(dispositivo.AssetTag)) return;

        var emUso = await _db.Dispositivos.IgnoreQueryFilters()
            .AnyAsync(d => d.AssetTag == patrimonio && d.Id != dispositivo.Id);
        if (emUso) return;

        dispositivo.AssetTag = patrimonio;
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
        // Heartbeat não é recusado por enum fora do intervalo (isso cortaria também a entrega de
        // comandos de bloqueio): o valor desconhecido é só ignorado.
        if (request.NivelProtecao is { } nivel && Enum.IsDefined(nivel))
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
                Origem = request.Origem is { } origem && Enum.IsDefined(origem)
                    ? origem
                    : OrigemLocalizacao.IpGeolocationWindows,
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

        // Estado desejado: o do ÚLTIMO comando de bloqueio/desbloqueio emitido (pendente, enviado ou
        // confirmado); só sem comando algum vale o status. O status só vira Bloqueado quando o
        // agente confirma — usá-lo sozinho fazia o heartbeat que entrega um Bloquear pendente
        // responder "desbloqueado", e o agente Android destravava logo depois de aplicar.
        var ultimoComando = await _db.UltimoComandoVigenteAsync(dispositivo.Id);
        var deveEstarBloqueado = RegrasDispositivo.DeveEstarBloqueado(dispositivo.Status, ultimoComando);

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

        var validation = await _alertaValidator.ValidateAsync(request);
        if (!validation.IsValid) return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));

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

        // Reflete o efeito do comando no status do dispositivo. O desbloqueio só tira de
        // Bloqueado (Baixado/Em manutenção ficam) e só volta a "Em uso" com responsável.
        // Um Bloquear confirmado depois da baixa não "desfaz" a baixa: o aparelho continua travado
        // (o estado de trava vem do último comando), mas segue fora do parque ativo.
        dispositivo.Status = comando.TipoComando == TipoComandoRemoto.Bloquear
            ? (dispositivo.Status == StatusDispositivo.Baixado ? StatusDispositivo.Baixado : StatusDispositivo.Bloqueado)
            : RegrasDispositivo.StatusAposDesbloqueio(dispositivo);

        await _db.SaveChangesAsync();
        return NoContent();
    }
}
