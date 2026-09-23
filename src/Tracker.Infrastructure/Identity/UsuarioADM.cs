using Microsoft.AspNetCore.Identity;

namespace Tracker.Infrastructure.Identity;

/// <summary>Profissional de TI com acesso à plataforma. Autenticação própria (email/senha) via ASP.NET Core Identity.</summary>
public class UsuarioADM : IdentityUser<Guid>
{
    public string Nome { get; set; } = string.Empty;
    public string Cpf { get; set; } = string.Empty;
    public DateTime? UltimoLoginEm { get; set; }

    /// <summary>
    /// Login habilitado. Desativar (em vez de excluir) preserva o histórico e a autoria que apontam
    /// para a conta. O login recusa quando falso; a sessão aberta cai via SecurityStamp.
    /// </summary>
    public bool Ativo { get; set; } = true;
}
