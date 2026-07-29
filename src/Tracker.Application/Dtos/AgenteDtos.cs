using Tracker.Domain.Enums;

namespace Tracker.Application.Dtos;

/// <summary>Token retornado uma única vez no enrollment do agente.</summary>
public record EnrollmentResponse(Guid DispositivoId, string Token);

/// <summary>
/// Auto-cadastro: o agente se registra sozinho na primeira execução, usando a chave da empresa
/// (header X-Enrollment-Key). O backend cria o dispositivo se ele ainda não existir.
/// </summary>
public record AutoEnrollmentRequest(
    string Hostname,
    string NumeroSerie,
    string SistemaOperacional,
    string? VersaoSO,
    string? MacAddress,
    TipoDispositivo Tipo,
    string? Imei);

/// <summary>Telemetria enviada periodicamente pelo agente.</summary>
public record HeartbeatRequest(
    string? Hostname,
    string? AgenteVersao,
    double? Latitude,
    double? Longitude,
    string? EnderecoAproximado,
    double? PrecisaoMetros);

/// <summary>Comando pendente entregue ao agente (via heartbeat ou SignalR).</summary>
public record ComandoPendenteDto(Guid Id, TipoComandoRemoto TipoComando);

/// <summary>Resposta do heartbeat: comandos que o agente deve executar agora.</summary>
public record HeartbeatResponse(IReadOnlyList<ComandoPendenteDto> ComandosPendentes);

public record EmitirComandoRequest(TipoComandoRemoto TipoComando);

public record ComandoRemotoDto(
    Guid Id,
    Guid DispositivoId,
    TipoComandoRemoto TipoComando,
    StatusComandoRemoto Status,
    Guid EmitidoPorAdmId,
    DateTime DataEmissao,
    DateTime? DataConfirmacao);
