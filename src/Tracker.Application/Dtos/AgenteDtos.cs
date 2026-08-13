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
    NivelProtecaoAgente? NivelProtecao = null);

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
    OrigemLocalizacao? Origem = null);

/// <summary>Comando pendente entregue ao agente (via heartbeat ou SignalR).</summary>
public record ComandoPendenteDto(Guid Id, TipoComandoRemoto TipoComando);

/// <summary>
/// Resposta do heartbeat: comandos que o agente deve executar agora.
/// <paramref name="DeveEstarBloqueado"/> é o estado desejado segundo o servidor — o agente
/// reaplica o bloqueio se tiver se perdido (app reinstalado, dados limpos) mesmo sem comando novo.
/// </summary>
public record HeartbeatResponse(
    IReadOnlyList<ComandoPendenteDto> ComandosPendentes,
    bool DeveEstarBloqueado = false);

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
