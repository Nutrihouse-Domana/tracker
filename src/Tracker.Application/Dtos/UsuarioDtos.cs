namespace Tracker.Application.Dtos;

public record UsuarioDto(
    Guid Id,
    string Nome,
    string Cpf,
    string Email,
    string Telefone,
    bool Ativo,
    int QuantidadeDispositivos);

public record CriarUsuarioRequest(
    string Nome,
    string Cpf,
    string Email,
    string Telefone);

public record AtualizarUsuarioRequest(
    string Nome,
    string Email,
    string Telefone,
    bool Ativo);
