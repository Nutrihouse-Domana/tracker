using Tracker.Domain.Enums;

namespace Tracker.Application.Dtos;

public record HistoricoReparoDto(
    Guid Id,
    Guid DispositivoId,
    string Descricao,
    StatusReparo Status,
    decimal? CustoEstimado,
    DateTime DataAbertura,
    DateTime? DataConclusao,
    Guid RegistradoPorAdmId);

public record CriarReparoRequest(
    string Descricao,
    decimal? CustoEstimado,
    Guid RegistradoPorAdmId);

public record AtualizarStatusReparoRequest(StatusReparo Status);
