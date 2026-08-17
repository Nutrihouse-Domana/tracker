using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tracker.Application.Dtos;
using Tracker.Domain.Entities;
using Tracker.Infrastructure.Data;

namespace Tracker.Api.Controllers;

/// <summary>Cadastro das unidades da Nutrihouse (matriz, filiais) que os dispositivos atendem.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UnidadesController : ControllerBase
{
    private readonly TrackerDbContext _db;
    private readonly IValidator<CriarUnidadeRequest> _criarValidator;
    private readonly IValidator<AtualizarUnidadeRequest> _atualizarValidator;

    public UnidadesController(
        TrackerDbContext db,
        IValidator<CriarUnidadeRequest> criarValidator,
        IValidator<AtualizarUnidadeRequest> atualizarValidator)
    {
        _db = db;
        _criarValidator = criarValidator;
        _atualizarValidator = atualizarValidator;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<UnidadeDto>>> Listar([FromQuery] bool incluirInativas = false)
    {
        var query = _db.Unidades.AsNoTracking();
        if (!incluirInativas) query = query.Where(u => u.Ativa);

        var unidades = await query
            .OrderBy(u => u.Nome)
            .Select(u => new UnidadeDto(u.Id, u.Nome, u.Codigo, u.Ativa, u.Dispositivos.Count))
            .ToListAsync();

        return Ok(unidades);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UnidadeDto>> ObterPorId(Guid id)
    {
        var unidade = await _db.Unidades.AsNoTracking()
            .Where(u => u.Id == id)
            .Select(u => new UnidadeDto(u.Id, u.Nome, u.Codigo, u.Ativa, u.Dispositivos.Count))
            .FirstOrDefaultAsync();

        return unidade is null ? NotFound() : Ok(unidade);
    }

    [HttpPost]
    public async Task<ActionResult<UnidadeDto>> Criar(CriarUnidadeRequest request)
    {
        var validation = await _criarValidator.ValidateAsync(request);
        if (!validation.IsValid) return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));

        if (await _db.Unidades.AnyAsync(u => u.Nome == request.Nome))
            return Conflict(new { message = "Já existe uma unidade com este nome." });

        var unidade = new Unidade
        {
            Nome = request.Nome.Trim(),
            Codigo = string.IsNullOrWhiteSpace(request.Codigo) ? null : request.Codigo.Trim(),
        };

        _db.Unidades.Add(unidade);
        await _db.SaveChangesAsync();

        var dto = new UnidadeDto(unidade.Id, unidade.Nome, unidade.Codigo, unidade.Ativa, 0);
        return CreatedAtAction(nameof(ObterPorId), new { id = unidade.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, AtualizarUnidadeRequest request)
    {
        var validation = await _atualizarValidator.ValidateAsync(request);
        if (!validation.IsValid) return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));

        var unidade = await _db.Unidades.FindAsync(id);
        if (unidade is null) return NotFound();

        if (await _db.Unidades.AnyAsync(u => u.Nome == request.Nome && u.Id != id))
            return Conflict(new { message = "Já existe outra unidade com este nome." });

        // Desativar unidade que ainda tem ativo do local deixaria esses dispositivos exibindo o
        // nome de uma unidade fora de uso — melhor barrar e obrigar a remanejar antes.
        if (!request.Ativa && unidade.Ativa)
        {
            var emUso = await _db.Dispositivos.CountAsync(d => d.UnidadeId == id);
            if (emUso > 0)
                return Conflict(new
                {
                    message = $"A unidade tem {emUso} dispositivo(s) vinculado(s). Remaneje-os antes de desativá-la.",
                });
        }

        unidade.Nome = request.Nome.Trim();
        unidade.Codigo = string.IsNullOrWhiteSpace(request.Codigo) ? null : request.Codigo.Trim();
        unidade.Ativa = request.Ativa;

        await _db.SaveChangesAsync();
        return NoContent();
    }
}
