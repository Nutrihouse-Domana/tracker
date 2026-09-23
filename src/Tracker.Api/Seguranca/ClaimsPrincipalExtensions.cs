using System.Security.Claims;

namespace Tracker.Api.Seguranca;

/// <summary>
/// Leitura da identidade do ADM logado a partir do principal. A autoria de qualquer ação vem daqui
/// (do token/cookie), NUNCA do corpo da requisição — que seria forjável agora que há vários logins.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>Id do ADM logado, ou null se não autenticado.</summary>
    public static Guid? AdmId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    /// <summary>Nome do ADM logado (claim "nome" injetado na transformação; cai para o e-mail).</summary>
    public static string AdmNome(this ClaimsPrincipal principal) =>
        principal.FindFirstValue("nome")
        ?? principal.Identity?.Name
        ?? "Desconhecido";
}
