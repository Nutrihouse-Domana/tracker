using Tracker.Domain.Enums;

namespace Tracker.Application.Dtos;

public record HistoricoLocalizacaoDto(
    Guid Id,
    Guid DispositivoId,
    double Latitude,
    double Longitude,
    string? EnderecoAproximado,
    OrigemLocalizacao Origem,
    double? PrecisaoMetros,
    DateTime CapturadoEm);

public record HistoricoMovimentacaoDto(
    Guid Id,
    Guid DispositivoId,
    Guid? UsuarioOrigemId,
    string? UsuarioOrigemNome,
    Guid? UsuarioDestinoId,
    string? UsuarioDestinoNome,
    Guid? LocalOrigemId,
    string? LocalOrigemNome,
    Guid? LocalDestinoId,
    string? LocalDestinoNome,
    DateTime DataMovimentacao,
    string? Motivo,
    string? RegistradoPorAdmNome = null);
