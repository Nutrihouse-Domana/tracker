using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tracker.Application.Dtos;
using Tracker.Domain.Entities;
using Tracker.Domain.Enums;
using Tracker.Infrastructure.Data;
using Tracker.Infrastructure.Identity;

namespace Tracker.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DispositivosController : ControllerBase
{
    private readonly TrackerDbContext _db;
    private readonly UserManager<UsuarioADM> _userManager;
    private readonly IValidator<CriarNotebookRequest> _notebookValidator;
    private readonly IValidator<CriarMobileRequest> _mobileValidator;
    private readonly IValidator<CriarPerifericoRequest> _perifericoValidator;
    private readonly IValidator<AtribuirDispositivoRequest> _atribuirValidator;
    private readonly IValidator<CriarReparoRequest> _reparoValidator;
    private readonly IValidator<CriarComentarioRequest> _comentarioValidator;

    public DispositivosController(
        TrackerDbContext db,
        UserManager<UsuarioADM> userManager,
        IValidator<CriarNotebookRequest> notebookValidator,
        IValidator<CriarMobileRequest> mobileValidator,
        IValidator<CriarPerifericoRequest> perifericoValidator,
        IValidator<AtribuirDispositivoRequest> atribuirValidator,
        IValidator<CriarReparoRequest> reparoValidator,
        IValidator<CriarComentarioRequest> comentarioValidator)
    {
        _db = db;
        _userManager = userManager;
        _notebookValidator = notebookValidator;
        _mobileValidator = mobileValidator;
        _perifericoValidator = perifericoValidator;
        _atribuirValidator = atribuirValidator;
        _reparoValidator = reparoValidator;
        _comentarioValidator = comentarioValidator;
    }

    private static DispositivoDto MapToDto(Dispositivo d) => new(
        d.Id,
        d.Tipo,
        d.NumeroSerie,
        d.AssetTag,
        d.Status,
        d.DataAquisicao,
        d.Observacoes,
        d.UsuarioAtualId,
        d.UsuarioAtual?.Nome,
        (d as DispositivoComAgente)?.Hostname,
        (d as DispositivoComAgente)?.SistemaOperacional,
        (d as DispositivoComAgente)?.VersaoSO,
        // IMEI vive em Mobile, não na base com agente (notebook não tem).
        (d as Mobile)?.Imei,
        (d as DispositivoComAgente)?.UltimoHeartbeatEm,
        (d as DispositivoComAgente)?.GeofenceAtivo,
        (d as DispositivoComAgente)?.AgenteVersao,
        (d as Periferico)?.TipoPeriferico,
        (d as DispositivoComAgente)?.Fabricante,
        (d as DispositivoComAgente)?.Modelo,
        (d as DispositivoComAgente)?.BateriaPercentual,
        (d as DispositivoComAgente)?.NivelProtecao);

    [HttpGet]
    public async Task<ActionResult<IEnumerable<DispositivoDto>>> Listar(
        [FromQuery] TipoDispositivo? tipo, [FromQuery] StatusDispositivo? status)
    {
        var query = _db.Dispositivos.AsNoTracking().Include(d => d.UsuarioAtual).AsQueryable();
        if (tipo is not null) query = query.Where(d => d.Tipo == tipo);
        if (status is not null) query = query.Where(d => d.Status == status);

        var dispositivos = await query.ToListAsync();
        return Ok(dispositivos.Select(MapToDto));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DispositivoDto>> ObterPorId(Guid id)
    {
        var dispositivo = await _db.Dispositivos.AsNoTracking()
            .Include(d => d.UsuarioAtual)
            .FirstOrDefaultAsync(d => d.Id == id);

        return dispositivo is null ? NotFound() : Ok(MapToDto(dispositivo));
    }

    [HttpPost("notebooks")]
    public async Task<ActionResult<DispositivoDto>> CriarNotebook(CriarNotebookRequest request)
    {
        var validation = await _notebookValidator.ValidateAsync(request);
        if (!validation.IsValid) return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));

        if (await _db.Dispositivos.AnyAsync(d => d.NumeroSerie == request.NumeroSerie || d.AssetTag == request.AssetTag))
            return Conflict(new { message = "Já existe um dispositivo com este número de série ou patrimônio." });

        var notebook = new Notebook
        {
            NumeroSerie = request.NumeroSerie,
            AssetTag = request.AssetTag,
            Hostname = request.Hostname,
            SistemaOperacional = request.SistemaOperacional,
            VersaoSO = request.VersaoSO,
            MacAddress = request.MacAddress,
            DataAquisicao = request.DataAquisicao,
            Observacoes = request.Observacoes,
        };

        _db.Dispositivos.Add(notebook);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(ObterPorId), new { id = notebook.Id }, MapToDto(notebook));
    }

    /// <summary>Cadastra um celular ou tablet. Rota antiga /telefones mantida como alias.</summary>
    [HttpPost("mobiles")]
    [HttpPost("telefones")]
    public async Task<ActionResult<DispositivoDto>> CriarMobile(CriarMobileRequest request)
    {
        var validation = await _mobileValidator.ValidateAsync(request);
        if (!validation.IsValid) return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));

        if (await _db.Dispositivos.AnyAsync(d => d.NumeroSerie == request.NumeroSerie || d.AssetTag == request.AssetTag))
            return Conflict(new { message = "Já existe um dispositivo com este número de série ou patrimônio." });

        var mobile = new Mobile
        {
            NumeroSerie = request.NumeroSerie,
            AssetTag = request.AssetTag,
            Hostname = request.Hostname,
            SistemaOperacional = request.SistemaOperacional,
            // Campo vazio no formulário significa "sem IMEI", não string vazia no banco.
            Imei = string.IsNullOrWhiteSpace(request.Imei) ? null : request.Imei,
            MacAddress = request.MacAddress,
            DataAquisicao = request.DataAquisicao,
            Observacoes = request.Observacoes,
        };

        _db.Dispositivos.Add(mobile);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(ObterPorId), new { id = mobile.Id }, MapToDto(mobile));
    }

    [HttpPost("perifericos")]
    public async Task<ActionResult<DispositivoDto>> CriarPeriferico(CriarPerifericoRequest request)
    {
        var validation = await _perifericoValidator.ValidateAsync(request);
        if (!validation.IsValid) return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));

        if (await _db.Dispositivos.AnyAsync(d => d.NumeroSerie == request.NumeroSerie))
            return Conflict(new { message = "Já existe um dispositivo com este número de série." });

        // Periférico não tem patrimônio: AssetTag fica nulo.
        var periferico = new Periferico
        {
            NumeroSerie = request.NumeroSerie,
            TipoPeriferico = request.TipoPeriferico,
            DataAquisicao = request.DataAquisicao,
            Observacoes = request.Observacoes,
        };

        _db.Dispositivos.Add(periferico);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(ObterPorId), new { id = periferico.Id }, MapToDto(periferico));
    }

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> AtualizarStatus(Guid id, AtualizarStatusDispositivoRequest request)
    {
        var dispositivo = await _db.Dispositivos.FindAsync(id);
        if (dispositivo is null) return NotFound();

        // "Em uso" exige detentor: marcar sem usuário deixaria a lista mentindo.
        if (request.Status == StatusDispositivo.EmUso && dispositivo.UsuarioAtualId is null)
            return BadRequest(new { message = "Atribua o dispositivo a um usuário para marcá-lo como em uso." });

        dispositivo.Status = request.Status;

        // Dispositivo em estoque ou baixado nao pertence a ninguem: libera o usuario atual
        // e registra a movimentacao de retorno (usuario -> estoque) no historico.
        if (request.Status is StatusDispositivo.EmEstoque or StatusDispositivo.Baixado)
        {
            if (dispositivo.UsuarioAtualId is not null)
            {
                _db.HistoricoMovimentacoes.Add(new HistoricoMovimentacao
                {
                    DispositivoId = dispositivo.Id,
                    UsuarioOrigemId = dispositivo.UsuarioAtualId,
                    UsuarioDestinoId = null, // destino nulo = estoque
                    Motivo = request.Status == StatusDispositivo.Baixado ? "Baixa do dispositivo" : "Devolucao ao estoque",
                    RegistradoPorAdmId = Guid.Parse(_userManager.GetUserId(User)!),
                });
            }
            dispositivo.UsuarioAtualId = null;
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Atribui o dispositivo a um usuário, registrando a movimentação (origem = posse/local anteriores).</summary>
    [HttpPost("{id:guid}/atribuir")]
    public async Task<ActionResult<HistoricoMovimentacaoDto>> Atribuir(Guid id, AtribuirDispositivoRequest request)
    {
        var validation = await _atribuirValidator.ValidateAsync(request);
        if (!validation.IsValid) return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));

        var dispositivo = await _db.Dispositivos.FirstOrDefaultAsync(d => d.Id == id);
        if (dispositivo is null) return NotFound(new { message = "Dispositivo não encontrado." });

        var usuarioDestino = await _db.Usuarios.FindAsync(request.UsuarioId);
        if (usuarioDestino is null) return NotFound(new { message = "Usuário destino não encontrado." });

        // Baixado/em manutenção não pode ganhar dono: senão o dispositivo ficaria com usuário
        // atribuído e status incoerente (ex.: "Baixado" com detentor).
        if (dispositivo.Status is StatusDispositivo.Baixado or StatusDispositivo.EmManutencao)
            return BadRequest(new
            {
                message = dispositivo.Status == StatusDispositivo.Baixado
                    ? "Dispositivo baixado. Devolva-o ao estoque antes de atribuir."
                    : "Dispositivo em manutenção. Conclua o reparo antes de atribuir.",
            });

        var movimentacao = new HistoricoMovimentacao
        {
            DispositivoId = dispositivo.Id,
            UsuarioOrigemId = dispositivo.UsuarioAtualId,
            UsuarioDestinoId = usuarioDestino.Id,
            LocalDestinoId = request.LocalDestinoId,
            Motivo = request.Motivo,
            RegistradoPorAdmId = request.RegistradoPorAdmId,
        };

        dispositivo.UsuarioAtualId = usuarioDestino.Id;
        // Ao ganhar dono, passa a "Em uso" — exceto se estiver bloqueado, estado de segurança
        // que só sai pelo comando remoto de desbloqueio.
        if (dispositivo.Status != StatusDispositivo.Bloqueado)
            dispositivo.Status = StatusDispositivo.EmUso;

        _db.HistoricoMovimentacoes.Add(movimentacao);
        await _db.SaveChangesAsync();

        var dto = new HistoricoMovimentacaoDto(
            movimentacao.Id, movimentacao.DispositivoId,
            movimentacao.UsuarioOrigemId, null,
            movimentacao.UsuarioDestinoId, usuarioDestino.Nome,
            movimentacao.LocalOrigemId, null,
            movimentacao.LocalDestinoId, null,
            movimentacao.DataMovimentacao, movimentacao.Motivo);

        return Ok(dto);
    }

    [HttpGet("{id:guid}/historico-movimentacao")]
    public async Task<ActionResult<IEnumerable<HistoricoMovimentacaoDto>>> HistoricoMovimentacao(Guid id)
    {
        var historico = await _db.HistoricoMovimentacoes.AsNoTracking()
            .Include(h => h.UsuarioOrigem)
            .Include(h => h.UsuarioDestino)
            .Include(h => h.LocalOrigem)
            .Include(h => h.LocalDestino)
            .Where(h => h.DispositivoId == id)
            .OrderByDescending(h => h.DataMovimentacao)
            .Select(h => new HistoricoMovimentacaoDto(
                h.Id, h.DispositivoId,
                h.UsuarioOrigemId, h.UsuarioOrigem != null ? h.UsuarioOrigem.Nome : null,
                h.UsuarioDestinoId, h.UsuarioDestino != null ? h.UsuarioDestino.Nome : null,
                h.LocalOrigemId, h.LocalOrigem != null ? h.LocalOrigem.Nome : null,
                h.LocalDestinoId, h.LocalDestino != null ? h.LocalDestino.Nome : null,
                h.DataMovimentacao, h.Motivo))
            .ToListAsync();

        return Ok(historico);
    }

    [HttpGet("{id:guid}/historico-localizacao")]
    public async Task<ActionResult<IEnumerable<HistoricoLocalizacaoDto>>> HistoricoLocalizacao(Guid id)
    {
        var historico = await _db.HistoricoLocalizacoes.AsNoTracking()
            .Where(h => h.DispositivoId == id)
            .OrderByDescending(h => h.CapturadoEm)
            .Take(200)
            .Select(h => new HistoricoLocalizacaoDto(
                h.Id, h.DispositivoId, h.Latitude, h.Longitude,
                h.EnderecoAproximado, h.Origem, h.PrecisaoMetros, h.CapturadoEm))
            .ToListAsync();

        return Ok(historico);
    }

    [HttpPost("{id:guid}/reparos")]
    public async Task<ActionResult<HistoricoReparoDto>> CriarReparo(Guid id, CriarReparoRequest request)
    {
        var validation = await _reparoValidator.ValidateAsync(request);
        if (!validation.IsValid) return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));

        var dispositivoExiste = await _db.Dispositivos.AnyAsync(d => d.Id == id);
        if (!dispositivoExiste) return NotFound(new { message = "Dispositivo não encontrado." });

        var reparo = new HistoricoReparo
        {
            DispositivoId = id,
            Descricao = request.Descricao,
            CustoEstimado = request.CustoEstimado,
            RegistradoPorAdmId = request.RegistradoPorAdmId,
        };

        _db.HistoricoReparos.Add(reparo);

        var dispositivo = await _db.Dispositivos.FirstAsync(d => d.Id == id);
        dispositivo.Status = StatusDispositivo.EmManutencao;

        await _db.SaveChangesAsync();

        return Ok(new HistoricoReparoDto(
            reparo.Id, reparo.DispositivoId, reparo.Descricao, reparo.Status,
            reparo.CustoEstimado, reparo.DataAbertura, reparo.DataConclusao, reparo.RegistradoPorAdmId));
    }

    [HttpGet("{id:guid}/reparos")]
    public async Task<ActionResult<IEnumerable<HistoricoReparoDto>>> ListarReparos(Guid id)
    {
        var reparos = await _db.HistoricoReparos.AsNoTracking()
            .Where(r => r.DispositivoId == id)
            .OrderByDescending(r => r.DataAbertura)
            .Select(r => new HistoricoReparoDto(
                r.Id, r.DispositivoId, r.Descricao, r.Status,
                r.CustoEstimado, r.DataAbertura, r.DataConclusao, r.RegistradoPorAdmId))
            .ToListAsync();

        return Ok(reparos);
    }

    [HttpPut("{id:guid}/reparos/{reparoId:guid}/status")]
    public async Task<IActionResult> AtualizarStatusReparo(Guid id, Guid reparoId, AtualizarStatusReparoRequest request)
    {
        var reparo = await _db.HistoricoReparos.FirstOrDefaultAsync(r => r.Id == reparoId && r.DispositivoId == id);
        if (reparo is null) return NotFound();

        reparo.Status = request.Status;
        if (request.Status is StatusReparo.Concluido or StatusReparo.Cancelado)
            reparo.DataConclusao = DateTime.UtcNow;

        if (request.Status == StatusReparo.Concluido)
        {
            var dispositivo = await _db.Dispositivos.FirstAsync(d => d.Id == id);
            dispositivo.Status = dispositivo.UsuarioAtualId is null ? StatusDispositivo.EmEstoque : StatusDispositivo.EmUso;
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id:guid}/comentarios")]
    public async Task<ActionResult<ComentarioDispositivoDto>> CriarComentario(Guid id, CriarComentarioRequest request)
    {
        var validation = await _comentarioValidator.ValidateAsync(request);
        if (!validation.IsValid) return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));

        var dispositivoExiste = await _db.Dispositivos.AnyAsync(d => d.Id == id);
        if (!dispositivoExiste) return NotFound(new { message = "Dispositivo não encontrado." });

        var admNome = await _db.Users.Where(u => u.Id == request.CriadoPorAdmId).Select(u => u.Nome).FirstOrDefaultAsync();
        if (admNome is null) return NotFound(new { message = "Usuario ADM não encontrado." });

        var comentario = new ComentarioDispositivo
        {
            DispositivoId = id,
            Texto = request.Texto,
            CriadoPorAdmId = request.CriadoPorAdmId,
        };

        _db.ComentariosDispositivo.Add(comentario);
        await _db.SaveChangesAsync();

        return Ok(new ComentarioDispositivoDto(
            comentario.Id, comentario.DispositivoId, comentario.Texto,
            comentario.CriadoPorAdmId, admNome, comentario.CriadoEm));
    }

    [HttpGet("{id:guid}/comentarios")]
    public async Task<ActionResult<IEnumerable<ComentarioDispositivoDto>>> ListarComentarios(Guid id)
    {
        var comentarios = await (
            from c in _db.ComentariosDispositivo.AsNoTracking()
            join u in _db.Users.AsNoTracking() on c.CriadoPorAdmId equals u.Id
            where c.DispositivoId == id
            orderby c.CriadoEm descending
            select new ComentarioDispositivoDto(c.Id, c.DispositivoId, c.Texto, c.CriadoPorAdmId, u.Nome, c.CriadoEm)
        ).ToListAsync();

        return Ok(comentarios);
    }
}
