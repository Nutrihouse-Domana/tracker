using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tracker.Api.Seguranca;
using Tracker.Application.Dtos;
using Tracker.Application.Seguranca;
using Tracker.Domain.Entities;
using Tracker.Domain.Enums;
using Tracker.Infrastructure.Data;

namespace Tracker.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsuariosController : ControllerBase
{
    private readonly TrackerDbContext _db;
    private readonly IValidator<CriarUsuarioRequest> _criarValidator;
    private readonly IValidator<AtualizarUsuarioRequest> _atualizarValidator;
    private readonly IAuditoria _auditoria;

    public UsuariosController(
        TrackerDbContext db,
        IValidator<CriarUsuarioRequest> criarValidator,
        IValidator<AtualizarUsuarioRequest> atualizarValidator,
        IAuditoria auditoria)
    {
        _db = db;
        _criarValidator = criarValidator;
        _atualizarValidator = atualizarValidator;
        _auditoria = auditoria;
    }

    // Listar detentores é preciso para os formulários de atribuição e termo, então a leitura pede
    // "ver dispositivos"; só o CRUD exige "gerir usuários".
    [HttpGet]
    [Authorize(Policy = Permissoes.DispositivosVer)]
    public async Task<ActionResult<IEnumerable<UsuarioDto>>> Listar([FromQuery] bool incluirInativos = false)
    {
        var query = _db.Usuarios.AsNoTracking();
        if (!incluirInativos) query = query.Where(u => u.Ativo);

        var usuarios = await query
            .Select(u => new UsuarioDto(u.Id, u.Nome, u.Cpf, u.Email, u.Telefone, u.Ativo, u.Dispositivos.Count))
            .ToListAsync();

        return Ok(usuarios);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Permissoes.DispositivosVer)]
    public async Task<ActionResult<UsuarioDto>> ObterPorId(Guid id)
    {
        var usuario = await _db.Usuarios.AsNoTracking()
            .Where(u => u.Id == id)
            .Select(u => new UsuarioDto(u.Id, u.Nome, u.Cpf, u.Email, u.Telefone, u.Ativo, u.Dispositivos.Count))
            .FirstOrDefaultAsync();

        return usuario is null ? NotFound() : Ok(usuario);
    }

    [HttpPost]
    [Authorize(Policy = Permissoes.UsuariosGerir)]
    public async Task<ActionResult<UsuarioDto>> Criar(CriarUsuarioRequest request)
    {
        var validation = await _criarValidator.ValidateAsync(request);
        if (!validation.IsValid) return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));

        if (await _db.Usuarios.AnyAsync(u => u.Email == request.Email))
            return Conflict(new { message = "Já existe um usuário com este email." });

        var usuario = new Usuario
        {
            Nome = request.Nome,
            Cpf = request.Cpf,
            Email = request.Email,
            Telefone = request.Telefone,
        };

        _db.Usuarios.Add(usuario);
        _auditoria.Registrar(AcaoAuditoria.DetentorCriado,
            $"Cadastrou o detentor {usuario.Nome}.", "Usuario", usuario.Id);
        await _db.SaveChangesAsync();

        var dto = new UsuarioDto(usuario.Id, usuario.Nome, usuario.Cpf, usuario.Email, usuario.Telefone, usuario.Ativo, 0);
        return CreatedAtAction(nameof(ObterPorId), new { id = usuario.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissoes.UsuariosGerir)]
    public async Task<IActionResult> Atualizar(Guid id, AtualizarUsuarioRequest request)
    {
        var validation = await _atualizarValidator.ValidateAsync(request);
        if (!validation.IsValid) return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));

        var usuario = await _db.Usuarios.FindAsync(id);
        if (usuario is null) return NotFound();

        if (await _db.Usuarios.AnyAsync(u => u.Email == request.Email && u.Id != id))
            return Conflict(new { message = "Já existe outro usuário com este email." });

        usuario.Nome = request.Nome;
        usuario.Email = request.Email;
        usuario.Telefone = request.Telefone;
        usuario.Ativo = request.Ativo;

        _auditoria.Registrar(AcaoAuditoria.DetentorEditado,
            $"Editou o detentor {usuario.Nome}.", "Usuario", usuario.Id);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissoes.UsuariosGerir)]
    public async Task<IActionResult> Desativar(Guid id)
    {
        var usuario = await _db.Usuarios.FindAsync(id);
        if (usuario is null) return NotFound();

        usuario.Ativo = false;
        _auditoria.Registrar(AcaoAuditoria.DetentorDesativado,
            $"Desativou o detentor {usuario.Nome}.", "Usuario", usuario.Id);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
