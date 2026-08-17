namespace Tracker.Application.Dtos;

public record UnidadeDto(
    Guid Id,
    string Nome,
    string? Codigo,
    bool Ativa,
    int QuantidadeDispositivos);

public record CriarUnidadeRequest(string Nome, string? Codigo);

public record AtualizarUnidadeRequest(string Nome, string? Codigo, bool Ativa);
