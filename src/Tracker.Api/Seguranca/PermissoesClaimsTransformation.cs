using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Tracker.Application.Seguranca;
using Tracker.Infrastructure.Identity;

namespace Tracker.Api.Seguranca;

/// <summary>
/// A cada requisição autenticada, carrega do banco as permissões (claims "perm") e o nome do ADM e
/// os injeta no principal. Isso mantém as permissões SEMPRE frescas: alterá-las para um usuário vale
/// já na próxima requisição dele, sem depender do que ficou gravado no cookie de login nem exigir
/// novo login. Um ADM inativo não recebe permissão nenhuma — toda policy passa a recusar (403).
/// </summary>
public class PermissoesClaimsTransformation : IClaimsTransformation
{
    // Marca que este principal já foi processado: a IClaimsTransformation pode ser chamada mais de
    // uma vez por requisição, e sem a guarda os claims seriam duplicados.
    private const string MarcadorProcessado = "perm_carregada";

    private readonly UserManager<UsuarioADM> _userManager;

    public PermissoesClaimsTransformation(UserManager<UsuarioADM> userManager)
    {
        _userManager = userManager;
    }

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
            return principal;

        if (identity.HasClaim(c => c.Type == MarcadorProcessado))
            return principal;

        // CRÍTICO: o factory do Identity grava as user-claims (inclusive as permissões) DENTRO do
        // cookie no login. Se apenas adicionássemos as permissões frescas por cima, as antigas do
        // cookie sobreviveriam — e revogar uma permissão só valeria após novo login (falha de
        // segurança). Removemos toda permissão presente e reescrevemos a partir do banco, que é a
        // única fonte de verdade. Assim a mudança vale já na próxima requisição, sem relogar.
        foreach (var claim in identity.FindAll(Permissoes.TipoClaim).ToList())
            identity.RemoveClaim(claim);

        var usuario = await _userManager.GetUserAsync(principal);
        if (usuario is null)
            return principal; // sem usuário: fica sem permissão alguma (as policies recusam)

        identity.AddClaim(new Claim(MarcadorProcessado, "1"));

        if (!string.IsNullOrWhiteSpace(usuario.Nome))
            identity.AddClaim(new Claim("nome", usuario.Nome));

        // Login desativado não recupera permissão (já removidas acima). A queda da sessão fica a
        // cargo do SecurityStamp; aqui garantimos que ele não aja enquanto isso.
        if (!usuario.Ativo)
            return principal;

        var claims = await _userManager.GetClaimsAsync(usuario);
        foreach (var claim in claims.Where(c => c.Type == Permissoes.TipoClaim))
            identity.AddClaim(new Claim(Permissoes.TipoClaim, claim.Value));

        return principal;
    }
}
