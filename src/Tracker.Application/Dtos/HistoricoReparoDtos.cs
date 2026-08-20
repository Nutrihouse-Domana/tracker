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
    Guid RegistradoPorAdmId,
    string? RegistradoPorAdmNome = null);

// Autoria vem do ADM logado no servidor, não do corpo.
public record CriarReparoRequest(
    string Descricao,
    decimal? CustoEstimado);

public record AtualizarStatusReparoRequest(StatusReparo Status);
