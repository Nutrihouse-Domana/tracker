namespace Tracker.Application.Dtos;

public record LoginRequest(string Email, string Senha);

public record UsuarioAdmDto(Guid Id, string Nome, string Email, IReadOnlyList<string> Roles);
