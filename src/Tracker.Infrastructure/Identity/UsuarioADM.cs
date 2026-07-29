using Microsoft.AspNetCore.Identity;

namespace Tracker.Infrastructure.Identity;

/// <summary>Profissional de TI com acesso à plataforma. Autenticação própria (email/senha) via ASP.NET Core Identity.</summary>
public class UsuarioADM : IdentityUser<Guid>
{
    public string Nome { get; set; } = string.Empty;
    public string Cpf { get; set; } = string.Empty;
    public DateTime? UltimoLoginEm { get; set; }
}
