using Tracker.Domain.Enums;

namespace Tracker.Application.Dtos;

/// <summary>
/// Token retornado uma única vez no enrollment do agente. O patrimônio acompanha para o agente
/// móvel poder exibi-lo na tela de bloqueio, permitindo à TI identificar o aparelho sem destravá-lo.
/// </summary>
public record EnrollmentResponse(Guid DispositivoId, string Token, string? AssetTag = null);

/// <summary>
/// Auto-cadastro: o agente se registra sozinho na primeira execução, usando a chave da empresa
/// (header X-Enrollment-Key). O backend cria o dispositivo se ele ainda não existir.
/// </summary>
/// Os campos a partir de Fabricante são opcionais: o agente Windows não os envia.
public record AutoEnrollmentRequest(
    string Hostname,
    string NumeroSerie,
    string SistemaOperacional,
    string? VersaoSO,
    string? MacAddress,
    TipoDispositivo Tipo,
    string? Imei,
    string? Fabricante = null,
    string? Modelo = null,
    NivelProtecaoAgente? NivelProtecao = null,
    /// <summary>
    /// Patrimônio digitado pelo técnico. É a identidade corporativa do ativo (está na etiqueta) e
    /// tem precedência no reencontro: diferente do número de série de aparelho sem Device Owner,
    /// ele não muda quando o app é reinstalado com outra assinatura.
    /// </summary>
    string? AssetTag = null);

/// <summary>
/// Telemetria enviada periodicamente pelo agente. Os campos a partir de BateriaPercentual só
/// chegam do agente Android; o agente Windows os omite e eles ficam nulos.
/// </summary>
public record HeartbeatRequest(
    string? Hostname,
    string? AgenteVersao,
    double? Latitude,
    double? Longitude,
    string? EnderecoAproximado,
    double? PrecisaoMetros,
    int? BateriaPercentual = null,
    NivelProtecaoAgente? NivelProtecao = null,
    OrigemLocalizacao? Origem = null,
    // versionCode do APK instalado: é o que dispara a oferta de atualização OTA. Só Android envia.
    int? AgenteVersaoCode = null,
    // IMEI lido no aparelho (disponível com Device Owner). Preenche o cadastro quando estiver vazio.
    string? Imei = null);

/// <summary>Comando pendente entregue ao agente (via heartbeat ou SignalR).</summary>
public record ComandoPendenteDto(Guid Id, TipoComandoRemoto TipoComando);

/// <summary>
/// Resposta do heartbeat: comandos que o agente deve executar agora.
/// <paramref name="DeveEstarBloqueado"/> é o estado desejado segundo o servidor — o agente
/// reaplica o bloqueio se tiver se perdido (app reinstalado, dados limpos) mesmo sem comando novo.
/// </summary>
public record HeartbeatResponse(
    IReadOnlyList<ComandoPendenteDto> ComandosPendentes,
    bool DeveEstarBloqueado = false,
    // Presente quando há APK mais novo publicado: o agente baixa, confere o hash e se instala.
    AtualizacaoAgenteDto? AtualizacaoAgente = null);

/// <summary>
/// Oferta de atualização OTA entregue no heartbeat. O agente confere o SHA-256 do APK baixado
/// contra <paramref name="HashSha256"/> antes de instalar — um download corrompido é descartado.
/// </summary>
public record AtualizacaoAgenteDto(int VersaoCode, string VersaoNome, string HashSha256, long TamanhoBytes);

/// <summary>Pacote OTA publicado, exibido no painel (o arquivo em si fica em disco).</summary>
public record PacoteAgenteAndroidDto(
    Guid Id,
    int VersaoCode,
    string VersaoNome,
    string NomeArquivoOriginal,
    string HashSha256,
    long TamanhoBytes,
    DateTime CriadoEm);

/// <summary>Situação da frota Android em relação ao pacote vigente.</summary>
public record SituacaoAgenteAndroidDto(
    PacoteAgenteAndroidDto? PacoteVigente,
    IReadOnlyList<PacoteAgenteAndroidDto> Historico,
    int DispositivosNaVersao,
    int DispositivosDesatualizados);

/// <summary>Evento que o agente reporta por conta própria (ex.: admin desativado no Android).</summary>
public record AlertaAgenteRequest(TipoAlertaDispositivo Tipo, string? Detalhe);

public record AlertaDispositivoDto(
    Guid Id,
    Guid DispositivoId,
    TipoAlertaDispositivo Tipo,
    string? Detalhe,
    bool Reconhecido,
    DateTime CriadoEm,
    DateTime? ReconhecidoEm);

public record EmitirComandoRequest(TipoComandoRemoto TipoComando);

public record ComandoRemotoDto(
    Guid Id,
    Guid DispositivoId,
    TipoComandoRemoto TipoComando,
    StatusComandoRemoto Status,
    Guid EmitidoPorAdmId,
    DateTime DataEmissao,
    DateTime? DataConfirmacao);
