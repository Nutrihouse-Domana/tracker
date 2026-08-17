using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Tracker.Infrastructure.Identity;

namespace Tracker.Api.Seguranca;

/// <summary>
/// Reconfere a senha do ADM já logado antes de uma ação sensível (excluir dispositivo, trocar a
/// posse de um ativo). Protege contra estação destravada: o cookie de sessão sozinho não basta.
/// </summary>
public class ConfirmacaoSenhaAdm
{
    private readonly UserManager<UsuarioADM> _userManager;
    private readonly ILogger<ConfirmacaoSenhaAdm> _logger;

    public ConfirmacaoSenhaAdm(UserManager<UsuarioADM> userManager, ILogger<ConfirmacaoSenhaAdm> logger)
    {
        _userManager = userManager;
        _logger = logger;
    }

    /// <summary>
    /// Confere a senha do ADM logado. Devolve o ADM quando confere, ou null quando não.
    /// </summary>
    public async Task<UsuarioADM?> ConferirAsync(ClaimsPrincipal principal, string? senha)
    {
        if (string.IsNullOrEmpty(senha)) return null;

        var adm = await _userManager.GetUserAsync(principal);
        if (adm is null) return null;

        if (!await _userManager.CheckPasswordAsync(adm, senha))
        {
            _logger.LogWarning("Confirmação por senha recusada para o ADM {AdmId}.", adm.Id);
            return null;
        }

        return adm;
    }
}
