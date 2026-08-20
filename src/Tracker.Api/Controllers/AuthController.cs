using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Tracker.Api.Seguranca;
using Tracker.Application.Dtos;
using Tracker.Application.Seguranca;
using Tracker.Domain.Enums;
using Tracker.Infrastructure.Data;
using Tracker.Infrastructure.Identity;

namespace Tracker.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly SignInManager<UsuarioADM> _signInManager;
    private readonly UserManager<UsuarioADM> _userManager;
    private readonly TrackerDbContext _db;
    private readonly IAuditoria _auditoria;

    public AuthController(
        SignInManager<UsuarioADM> signInManager,
        UserManager<UsuarioADM> userManager,
        TrackerDbContext db,
        IAuditoria auditoria)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _db = db;
        _auditoria = auditoria;
    }

    [HttpPost("login")]
    public async Task<ActionResult<UsuarioAdmDto>> Login(LoginRequest request)
    {
        var usuario = await _userManager.FindByEmailAsync(request.Email);
        if (usuario is null) return Unauthorized(new { message = "Credenciais inválidas." });

        // Login desativado não entra — mesmo com a senha certa. Mensagem própria para não confundir
        // com senha errada (a TI precisa saber que a conta foi desligada, não que errou a senha).
        if (!usuario.Ativo)
        {
            _auditoria.Registrar(AcaoAuditoria.LoginRecusado,
                $"Login recusado: conta desativada ({usuario.Email}).", "Acesso", usuario.Id);
            await _db.SaveChangesAsync();
            return Unauthorized(new { message = "Este login está desativado. Fale com um administrador." });
        }

        var resultado = await _signInManager.PasswordSignInAsync(usuario, request.Senha, isPersistent: true, lockoutOnFailure: true);
        if (!resultado.Succeeded)
        {
            _auditoria.Registrar(AcaoAuditoria.LoginRecusado,
                $"Login malsucedido ({request.Email}).", "Acesso", usuario.Id);
            await _db.SaveChangesAsync();
            return Unauthorized(new { message = "Credenciais inválidas." });
        }

        usuario.UltimoLoginEm = DateTime.UtcNow;
        await _userManager.UpdateAsync(usuario);

        _auditoria.Registrar(AcaoAuditoria.LoginEfetuado, $"Login efetuado ({usuario.Email}).", "Acesso", usuario.Id);
        await _db.SaveChangesAsync();

        return Ok(await MontarDtoAsync(usuario));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UsuarioAdmDto>> Me()
    {
        var usuario = await _userManager.GetUserAsync(User);
        if (usuario is null) return Unauthorized();

        return Ok(await MontarDtoAsync(usuario));
    }

    private async Task<UsuarioAdmDto> MontarDtoAsync(UsuarioADM usuario)
    {
        var roles = await _userManager.GetRolesAsync(usuario);
        var permissoes = (await _userManager.GetClaimsAsync(usuario))
            .Where(c => c.Type == Permissoes.TipoClaim)
            .Select(c => c.Value)
            .ToList();

        return new UsuarioAdmDto(usuario.Id, usuario.Nome, usuario.Email!, roles.ToList(), permissoes);
    }
}
