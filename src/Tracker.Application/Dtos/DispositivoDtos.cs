using Tracker.Domain.Enums;

namespace Tracker.Application.Dtos;

public record DispositivoDto(
    Guid Id,
    TipoDispositivo Tipo,
    string NumeroSerie,
    string? AssetTag,
    StatusDispositivo Status,
    DateTime DataAquisicao,
    string? Observacoes,
    Guid? UsuarioAtualId,
    string? UsuarioAtualNome,
    string? Hostname,
    string? SistemaOperacional,
    string? VersaoSO,
    string? Imei,
    DateTime? UltimoHeartbeatEm,
    bool? GeofenceAtivo,
    string? AgenteVersao,
    TipoPeriferico? TipoPeriferico,
    string? Fabricante = null,
    string? Modelo = null,
    int? BateriaPercentual = null,
    NivelProtecaoAgente? NivelProtecao = null);

public record CriarNotebookRequest(
    string NumeroSerie,
    string AssetTag,
    string Hostname,
    string SistemaOperacional,
    string? VersaoSO,
    string? MacAddress,
    DateTime DataAquisicao,
    string? Observacoes);

/// <summary>Celular ou tablet. IMEI é opcional porque tablets só-Wi-Fi não têm.</summary>
public record CriarMobileRequest(
    string NumeroSerie,
    string AssetTag,
    string Hostname,
    string SistemaOperacional,
    string? Imei,
    string? MacAddress,
    DateTime DataAquisicao,
    string? Observacoes);

public record CriarPerifericoRequest(
    string NumeroSerie,
    TipoPeriferico TipoPeriferico,
    DateTime DataAquisicao,
    string? Observacoes);

public record AtualizarStatusDispositivoRequest(StatusDispositivo Status);

public record AtribuirDispositivoRequest(
    Guid UsuarioId,
    Guid? LocalDestinoId,
    string? Motivo,
    Guid RegistradoPorAdmId);
