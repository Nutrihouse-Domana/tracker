namespace Tracker.Application.Dtos;

/// <summary>Um login (UsuarioADM) na tela de Acessos, com suas permissões.</summary>
public record AcessoDto(
    Guid Id,
    string Nome,
    string Email,
    bool Ativo,
    DateTime? UltimoLoginEm,
    IReadOnlyList<string> Permissoes);

/// <summary>Metadados de uma permissão para montar os checkboxes agrupados na UI.</summary>
public record PermissaoDto(string Valor, string Rotulo, string Descricao, string Grupo);

/// <summary>
/// Cria um novo login. <c>SenhaAdm</c> é a senha do ADM logado (reconferência da ação sensível),
/// não a senha do novo usuário — essa vem em <c>Senha</c>.
/// </summary>
public record CriarAcessoRequest(
    string Nome,
    string Email,
    string? Cpf,
    string Senha,
    IReadOnlyList<string> Permissoes,
    string SenhaAdm);

/// <summary>Edita nome/e-mail/CPF e o conjunto de permissões de um login.</summary>
public record AtualizarAcessoRequest(
    string Nome,
    string Email,
    string? Cpf,
    IReadOnlyList<string> Permissoes,
    string SenhaAdm);

public record AlterarAtivoAcessoRequest(bool Ativo, string SenhaAdm);

public record RedefinirSenhaAcessoRequest(string NovaSenha, string SenhaAdm);
