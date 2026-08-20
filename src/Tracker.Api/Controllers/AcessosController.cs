using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tracker.Api.Seguranca;
using Tracker.Application.Dtos;
using Tracker.Application.Seguranca;
using Tracker.Domain.Enums;
using Tracker.Infrastructure.Data;
using Tracker.Infrastructure.Identity;

namespace Tracker.Api.Controllers;

/// <summary>
/// Gestão dos LOGINS da plataforma (UsuarioADM) e suas permissões. Protegido pela permissão-mestra
/// <see cref="Permissoes.AcessosGerir"/>. Não confundir com <c>UsuariosController</c>, que trata dos
/// detentores de dispositivos (sem login).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = Permissoes.AcessosGerir)]
public class AcessosController : ControllerBase
{
    private readonly UserManager<UsuarioADM> _userManager;
    private readonly TrackerDbContext _db;
    private readonly ConfirmacaoSenhaAdm _confirmacaoSenha;
    private readonly IAuditoria _auditoria;

    public AcessosController(
        UserManager<UsuarioADM> userManager,
        TrackerDbContext db,
        ConfirmacaoSenhaAdm confirmacaoSenha,
        IAuditoria auditoria)
    {
        _userManager = userManager;
        _db = db;
        _confirmacaoSenha = confirmacaoSenha;
        _auditoria = auditoria;
    }

    /// <summary>Catálogo de permissões (valor + rótulo + descrição + grupo) para montar os checkboxes.</summary>
    [HttpGet("permissoes")]
    public ActionResult<IEnumerable<PermissaoDto>> ListarPermissoes() =>
        Ok(Permissoes.Catalogo.Select(d => new PermissaoDto(d.Valor, d.Rotulo, d.Descricao, d.Grupo)));

    [HttpGet]
    public async Task<ActionResult<IEnumerable<AcessoDto>>> Listar()
    {
        // Uma consulta só para as permissões de todos (evita N+1 e o uso concorrente do DbContext,
        // que um Task.WhenAll sobre GetClaimsAsync provoca — "second operation on this context").
        var usuarios = await _userManager.Users.OrderBy(u => u.Nome).ToListAsync();
        var ids = usuarios.Select(u => u.Id).ToList();

        var permsPorUsuario = await _db.UserClaims
            .Where(c => c.ClaimType == Permissoes.TipoClaim && ids.Contains(c.UserId))
            .GroupBy(c => c.UserId)
            .Select(g => new { UserId = g.Key, Perms = g.Select(c => c.ClaimValue!).ToList() })
            .ToDictionaryAsync(x => x.UserId, x => x.Perms);

        var dtos = usuarios.Select(u => new AcessoDto(
            u.Id, u.Nome, u.Email!, u.Ativo, u.UltimoLoginEm,
            permsPorUsuario.TryGetValue(u.Id, out var p) ? p : new List<string>()));

        return Ok(dtos);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AcessoDto>> ObterPorId(Guid id)
    {
        var usuario = await _userManager.FindByIdAsync(id.ToString());
        return usuario is null ? NotFound() : Ok(await MontarDtoAsync(usuario));
    }

    [HttpPost]
    public async Task<ActionResult<AcessoDto>> Criar(CriarAcessoRequest request)
    {
        if (await _confirmacaoSenha.ConferirAsync(User, request.SenhaAdm) is null)
            return BadRequest(new { message = "Senha incorreta." });

        var erroPerms = ValidarPermissoes(request.Permissoes);
        if (erroPerms is not null) return BadRequest(new { message = erroPerms });

        if (string.IsNullOrWhiteSpace(request.Nome))
            return BadRequest(new { message = "Informe o nome." });

        var usuario = new UsuarioADM
        {
            UserName = request.Email.Trim(),
            Email = request.Email.Trim(),
            EmailConfirmed = true,
            Nome = request.Nome.Trim(),
            Cpf = request.Cpf?.Trim() ?? string.Empty,
            Ativo = true,
        };

        var criacao = await _userManager.CreateAsync(usuario, request.Senha);
        if (!criacao.Succeeded) return BadRequest(new { message = JuntarErros(criacao) });

        await DefinirPermissoesAsync(usuario, request.Permissoes);

        _auditoria.Registrar(AcaoAuditoria.AcessoCriado,
            $"Criou o login {usuario.Nome} ({usuario.Email}).", "Acesso", usuario.Id,
            $"Permissões: {DescreverPermissoes(request.Permissoes)}");
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(ObterPorId), new { id = usuario.Id }, await MontarDtoAsync(usuario));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, AtualizarAcessoRequest request)
    {
        if (await _confirmacaoSenha.ConferirAsync(User, request.SenhaAdm) is null)
            return BadRequest(new { message = "Senha incorreta." });

        var erroPerms = ValidarPermissoes(request.Permissoes);
        if (erroPerms is not null) return BadRequest(new { message = erroPerms });

        var usuario = await _userManager.FindByIdAsync(id.ToString());
        if (usuario is null) return NotFound();

        var tinhaGestao = await UsuarioTemPermissaoAsync(usuario, Permissoes.AcessosGerir);
        var perderaGestao = tinhaGestao && !request.Permissoes.Contains(Permissoes.AcessosGerir);

        // Autoproteção: ninguém remove a PRÓPRIA permissão de gerir acessos (auto-lockout). Outro
        // administrador tem que fazer isso — assim quem edita não se tranca fora por engano.
        if (perderaGestao && usuario.Id == User.AdmId())
            return BadRequest(new { message = "Você não pode remover a sua própria permissão de gerir acessos. Peça a outro administrador." });

        // Anti-lockout global: não deixar remover a última permissão de gestão de acessos ativa —
        // senão ninguém mais consegue administrar logins.
        if (perderaGestao && !await OutroGestorAtivoExisteAsync(usuario.Id))
            return BadRequest(new { message = "Este é o único administrador ativo de acessos. Dê a permissão a outra pessoa antes de retirá-la aqui." });

        usuario.Nome = request.Nome.Trim();
        usuario.Email = request.Email.Trim();
        usuario.UserName = request.Email.Trim();
        if (request.Cpf is not null) usuario.Cpf = request.Cpf.Trim();

        var atualizacao = await _userManager.UpdateAsync(usuario);
        if (!atualizacao.Succeeded) return BadRequest(new { message = JuntarErros(atualizacao) });

        await DefinirPermissoesAsync(usuario, request.Permissoes);

        _auditoria.Registrar(AcaoAuditoria.AcessoEditado,
            $"Editou o login {usuario.Nome} ({usuario.Email}).", "Acesso", usuario.Id);
        _auditoria.Registrar(AcaoAuditoria.AcessoPermissoesAlteradas,
            $"Definiu as permissões de {usuario.Nome}.", "Acesso", usuario.Id,
            $"Permissões: {DescreverPermissoes(request.Permissoes)}");
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpPut("{id:guid}/ativo")]
    public async Task<IActionResult> AlterarAtivo(Guid id, AlterarAtivoAcessoRequest request)
    {
        if (await _confirmacaoSenha.ConferirAsync(User, request.SenhaAdm) is null)
            return BadRequest(new { message = "Senha incorreta." });

        var usuario = await _userManager.FindByIdAsync(id.ToString());
        if (usuario is null) return NotFound();

        if (usuario.Ativo == request.Ativo) return NoContent();

        // Autoproteção: ninguém desativa o próprio login (auto-lockout).
        if (!request.Ativo && usuario.Id == User.AdmId())
            return BadRequest(new { message = "Você não pode desativar o seu próprio login." });

        // Desativar o último gestor ativo trancaria a administração de acessos.
        if (!request.Ativo
            && await UsuarioTemPermissaoAsync(usuario, Permissoes.AcessosGerir)
            && !await OutroGestorAtivoExisteAsync(usuario.Id))
            return BadRequest(new { message = "Este é o único administrador ativo de acessos. Não é possível desativá-lo." });

        usuario.Ativo = request.Ativo;
        await _userManager.UpdateAsync(usuario);
        // Derruba a sessão aberta do usuário desativado (o SecurityStampValidator recusa o cookie).
        await _userManager.UpdateSecurityStampAsync(usuario);

        _auditoria.Registrar(
            request.Ativo ? AcaoAuditoria.AcessoAtivado : AcaoAuditoria.AcessoDesativado,
            $"{(request.Ativo ? "Ativou" : "Desativou")} o login {usuario.Nome} ({usuario.Email}).",
            "Acesso", usuario.Id);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost("{id:guid}/redefinir-senha")]
    public async Task<IActionResult> RedefinirSenha(Guid id, RedefinirSenhaAcessoRequest request)
    {
        if (await _confirmacaoSenha.ConferirAsync(User, request.SenhaAdm) is null)
            return BadRequest(new { message = "Senha incorreta." });

        var usuario = await _userManager.FindByIdAsync(id.ToString());
        if (usuario is null) return NotFound();

        // Token + reset valida a política de senha e renova o SecurityStamp (invalida sessões antigas).
        var token = await _userManager.GeneratePasswordResetTokenAsync(usuario);
        var resultado = await _userManager.ResetPasswordAsync(usuario, token, request.NovaSenha);
        if (!resultado.Succeeded) return BadRequest(new { message = JuntarErros(resultado) });

        _auditoria.Registrar(AcaoAuditoria.AcessoSenhaRedefinida,
            $"Redefiniu a senha do login {usuario.Nome} ({usuario.Email}).", "Acesso", usuario.Id);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    // --- Auxiliares ---

    private async Task<AcessoDto> MontarDtoAsync(UsuarioADM usuario)
    {
        var permissoes = (await _userManager.GetClaimsAsync(usuario))
            .Where(c => c.Type == Permissoes.TipoClaim)
            .Select(c => c.Value)
            .ToList();

        return new AcessoDto(usuario.Id, usuario.Nome, usuario.Email!, usuario.Ativo, usuario.UltimoLoginEm, permissoes);
    }

    private async Task<bool> UsuarioTemPermissaoAsync(UsuarioADM usuario, string permissao) =>
        (await _userManager.GetClaimsAsync(usuario)).Any(c => c.Type == Permissoes.TipoClaim && c.Value == permissao);

    private async Task<bool> OutroGestorAtivoExisteAsync(Guid excetoId)
    {
        var gestores = await _userManager.GetUsersForClaimAsync(new Claim(Permissoes.TipoClaim, Permissoes.AcessosGerir));
        return gestores.Any(u => u.Id != excetoId && u.Ativo);
    }

    /// <summary>Substitui o conjunto de claims "perm" do usuário pelo informado.</summary>
    private async Task DefinirPermissoesAsync(UsuarioADM usuario, IReadOnlyList<string> permissoes)
    {
        var atuais = (await _userManager.GetClaimsAsync(usuario))
            .Where(c => c.Type == Permissoes.TipoClaim)
            .ToList();
        if (atuais.Count > 0) await _userManager.RemoveClaimsAsync(usuario, atuais);

        var novas = permissoes.Distinct().Select(p => new Claim(Permissoes.TipoClaim, p)).ToList();
        if (novas.Count > 0) await _userManager.AddClaimsAsync(usuario, novas);
    }

    private static string? ValidarPermissoes(IReadOnlyList<string> permissoes)
    {
        var invalida = permissoes.FirstOrDefault(p => !Permissoes.EhValida(p));
        return invalida is null ? null : $"Permissão desconhecida: {invalida}.";
    }

    private static string DescreverPermissoes(IReadOnlyList<string> permissoes) =>
        permissoes.Count == 0 ? "(nenhuma)" : string.Join(", ", permissoes);

    private static string JuntarErros(IdentityResult resultado) =>
        string.Join(" ", resultado.Errors.Select(e => e.Description));
}
