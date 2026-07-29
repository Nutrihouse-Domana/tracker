using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Tracker.Application.Dtos;
using Tracker.Infrastructure.Identity;

namespace Tracker.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly SignInManager<UsuarioADM> _signInManager;
    private readonly UserManager<UsuarioADM> _userManager;

    public AuthController(SignInManager<UsuarioADM> signInManager, UserManager<UsuarioADM> userManager)
    {
        _signInManager = signInManager;
        _userManager = userManager;
    }

    [HttpPost("login")]
    public async Task<ActionResult<UsuarioAdmDto>> Login(LoginRequest request)
    {
        var usuario = await _userManager.FindByEmailAsync(request.Email);
        if (usuario is null) return Unauthorized(new { message = "Credenciais inválidas." });

        var resultado = await _signInManager.PasswordSignInAsync(usuario, request.Senha, isPersistent: true, lockoutOnFailure: true);
        if (!resultado.Succeeded) return Unauthorized(new { message = "Credenciais inválidas." });

        usuario.UltimoLoginEm = DateTime.UtcNow;
        await _userManager.UpdateAsync(usuario);

        var roles = await _userManager.GetRolesAsync(usuario);
        return Ok(new UsuarioAdmDto(usuario.Id, usuario.Nome, usuario.Email!, roles.ToList()));
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

        var roles = await _userManager.GetRolesAsync(usuario);
        return Ok(new UsuarioAdmDto(usuario.Id, usuario.Nome, usuario.Email!, roles.ToList()));
    }
}
