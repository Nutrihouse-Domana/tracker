using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tracker.Application.Dtos;
using Tracker.Domain.Entities;
using Tracker.Domain.Enums;
using Tracker.Api.Seguranca;
using Tracker.Infrastructure.Data;
using Tracker.Infrastructure.Identity;
using Tracker.Infrastructure.Storage;

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
    private readonly IValidator<ExcluirDispositivoRequest> _excluirValidator;
    private readonly IValidator<DefinirPosseDispositivoRequest> _posseValidator;
    private readonly ConfirmacaoSenhaAdm _confirmacaoSenha;
    private readonly IArmazenamentoArquivosService _arquivos;
    private readonly ILogger<DispositivosController> _logger;

    public DispositivosController(
        TrackerDbContext db,
        UserManager<UsuarioADM> userManager,
        IValidator<CriarNotebookRequest> notebookValidator,
        IValidator<CriarMobileRequest> mobileValidator,
        IValidator<CriarPerifericoRequest> perifericoValidator,
        IValidator<AtribuirDispositivoRequest> atribuirValidator,
        IValidator<CriarReparoRequest> reparoValidator,
        IValidator<CriarComentarioRequest> comentarioValidator,
        IValidator<ExcluirDispositivoRequest> excluirValidator,
        IValidator<DefinirPosseDispositivoRequest> posseValidator,
        ConfirmacaoSenhaAdm confirmacaoSenha,
        IArmazenamentoArquivosService arquivos,
        ILogger<DispositivosController> logger)
    {
        _arquivos = arquivos;
        _logger = logger;
        _db = db;
        _userManager = userManager;
        _notebookValidator = notebookValidator;
        _mobileValidator = mobileValidator;
        _perifericoValidator = perifericoValidator;
        _atribuirValidator = atribuirValidator;
        _reparoValidator = reparoValidator;
        _comentarioValidator = comentarioValidator;
        _excluirValidator = excluirValidator;
        _posseValidator = posseValidator;
        _confirmacaoSenha = confirmacaoSenha;
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
        (d as DispositivoComAgente)?.NivelProtecao,
        d.UnidadeId,
        d.Unidade?.Nome,
        d.PertenceAUsuario,
        // Ativo do local não tem detentor: quem aparece no lugar do usuário é a unidade.
        d.PertenceAUsuario ? d.UsuarioAtual?.Nome : d.Unidade?.Nome,
        d.ExcluidoEm,
        d.MotivoExclusao);

    /// <summary>Dispositivos ativos. Os arquivados ficam de fora (filtro global) — veja /arquivados.</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<DispositivoDto>>> Listar(
        [FromQuery] TipoDispositivo? tipo, [FromQuery] StatusDispositivo? status, [FromQuery] Guid? unidadeId)
    {
        var query = _db.Dispositivos.AsNoTracking()
            .Include(d => d.UsuarioAtual)
            .Include(d => d.Unidade)
            .AsQueryable();
        if (tipo is not null) query = query.Where(d => d.Tipo == tipo);
        if (status is not null) query = query.Where(d => d.Status == status);
        if (unidadeId is not null) query = query.Where(d => d.UnidadeId == unidadeId);

        var dispositivos = await query.ToListAsync();
        return Ok(dispositivos.Select(MapToDto));
    }

    /// <summary>Dispositivos arquivados, para consulta e restauração pela TI.</summary>
    [HttpGet("arquivados")]
    public async Task<ActionResult<IEnumerable<DispositivoDto>>> ListarArquivados()
    {
        var dispositivos = await _db.Dispositivos.AsNoTracking()
            .IgnoreQueryFilters()
            .Include(d => d.UsuarioAtual)
            .Include(d => d.Unidade)
            .Where(d => d.ExcluidoEm != null)
            .OrderByDescending(d => d.ExcluidoEm)
            .ToListAsync();

        return Ok(dispositivos.Select(MapToDto));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DispositivoDto>> ObterPorId(Guid id)
    {
        var dispositivo = await _db.Dispositivos.AsNoTracking()
            .Include(d => d.UsuarioAtual)
            .Include(d => d.Unidade)
            .FirstOrDefaultAsync(d => d.Id == id);

        return dispositivo is null ? NotFound() : Ok(MapToDto(dispositivo));
    }

    /// <summary>
    /// Confere série/patrimônio duplicados INCLUSIVE contra dispositivos arquivados: o índice
    /// único do banco não conhece o filtro de exclusão lógica, e sem olhar os arquivados o
    /// cadastro estouraria com erro cru de banco em vez de uma mensagem compreensível.
    /// </summary>
    private async Task<string?> ConferirDuplicidadeAsync(string numeroSerie, string? assetTag)
    {
        var conflitante = await _db.Dispositivos.AsNoTracking().IgnoreQueryFilters()
            .Where(d => d.NumeroSerie == numeroSerie || (assetTag != null && d.AssetTag == assetTag))
            .Select(d => new { d.ExcluidoEm })
            .FirstOrDefaultAsync();

        if (conflitante is null) return null;

        return conflitante.ExcluidoEm is null
            ? "Já existe um dispositivo com este número de série ou patrimônio."
            : "Já existe um dispositivo ARQUIVADO com este número de série ou patrimônio. Restaure-o em vez de cadastrar de novo.";
    }

    /// <summary>Valida a unidade informada. Devolve a mensagem de erro, ou null quando está tudo certo.</summary>
    private async Task<string?> ConferirUnidadeAsync(Guid? unidadeId, bool pertenceAUsuario)
    {
        if (!pertenceAUsuario && unidadeId is null)
            return "Dispositivo do local precisa de uma unidade: é o nome dela que aparece no lugar do usuário.";

        if (unidadeId is null) return null;

        return await _db.Unidades.AnyAsync(u => u.Id == unidadeId && u.Ativa)
            ? null
            : "Unidade não encontrada ou inativa.";
    }

    [HttpPost("notebooks")]
    public async Task<ActionResult<DispositivoDto>> CriarNotebook(CriarNotebookRequest request)
    {
        var validation = await _notebookValidator.ValidateAsync(request);
        if (!validation.IsValid) return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));

        var duplicidade = await ConferirDuplicidadeAsync(request.NumeroSerie, request.AssetTag);
        if (duplicidade is not null) return Conflict(new { message = duplicidade });

        var erroUnidade = await ConferirUnidadeAsync(request.UnidadeId, request.PertenceAUsuario);
        if (erroUnidade is not null) return BadRequest(new { message = erroUnidade });

        var notebook = new Notebook
        {
            UnidadeId = request.UnidadeId,
            PertenceAUsuario = request.PertenceAUsuario,
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

        var duplicidade = await ConferirDuplicidadeAsync(request.NumeroSerie, request.AssetTag);
        if (duplicidade is not null) return Conflict(new { message = duplicidade });

        var erroUnidade = await ConferirUnidadeAsync(request.UnidadeId, request.PertenceAUsuario);
        if (erroUnidade is not null) return BadRequest(new { message = erroUnidade });

        var mobile = new Mobile
        {
            UnidadeId = request.UnidadeId,
            PertenceAUsuario = request.PertenceAUsuario,
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

        var duplicidade = await ConferirDuplicidadeAsync(request.NumeroSerie, assetTag: null);
        if (duplicidade is not null) return Conflict(new { message = duplicidade });

        var erroUnidade = await ConferirUnidadeAsync(request.UnidadeId, request.PertenceAUsuario);
        if (erroUnidade is not null) return BadRequest(new { message = erroUnidade });

        // Periférico não tem patrimônio: AssetTag fica nulo.
        var periferico = new Periferico
        {
            UnidadeId = request.UnidadeId,
            PertenceAUsuario = request.PertenceAUsuario,
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

        // "Em uso" exige um responsável: usuário nominal, ou a unidade quando o ativo é do local.
        // Sem isso a lista mentiria, mostrando "em uso" sem dizer por quem.
        if (request.Status == StatusDispositivo.EmUso)
        {
            if (dispositivo.PertenceAUsuario && dispositivo.UsuarioAtualId is null)
                return BadRequest(new { message = "Atribua o dispositivo a um usuário para marcá-lo como em uso." });

            if (!dispositivo.PertenceAUsuario && dispositivo.UnidadeId is null)
                return BadRequest(new { message = "Defina a unidade do dispositivo para marcá-lo como em uso." });
        }

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

        // Ativo do local não tem detentor individual: atribuir aqui contradiria o interruptor.
        if (!dispositivo.PertenceAUsuario)
            return BadRequest(new
            {
                message = "Este dispositivo pertence à unidade, não a um usuário. "
                          + "Ative \"Pertence a usuário\" antes de atribuí-lo.",
            });

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

    /// <summary>Define (ou limpa) a unidade que o dispositivo atende.</summary>
    [HttpPut("{id:guid}/unidade")]
    public async Task<IActionResult> DefinirUnidade(Guid id, DefinirUnidadeDispositivoRequest request)
    {
        var dispositivo = await _db.Dispositivos.FirstOrDefaultAsync(d => d.Id == id);
        if (dispositivo is null) return NotFound();

        var erroUnidade = await ConferirUnidadeAsync(request.UnidadeId, dispositivo.PertenceAUsuario);
        if (erroUnidade is not null) return BadRequest(new { message = erroUnidade });

        dispositivo.UnidadeId = request.UnidadeId;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Alterna entre "pertence a um usuário" e "pertence ao local (unidade)". Exige a senha do
    /// ADM logado: desligar o interruptor solta o detentor atual do ativo.
    /// </summary>
    [HttpPut("{id:guid}/posse")]
    public async Task<IActionResult> DefinirPosse(Guid id, DefinirPosseDispositivoRequest request)
    {
        var validation = await _posseValidator.ValidateAsync(request);
        if (!validation.IsValid) return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));

        var adm = await _confirmacaoSenha.ConferirAsync(User, request.Senha);
        // 400, e não 401: o 401 faria o site tratar como sessão expirada e deslogar o ADM.
        if (adm is null) return BadRequest(new { message = "Senha incorreta." });

        var dispositivo = await _db.Dispositivos.FirstOrDefaultAsync(d => d.Id == id);
        if (dispositivo is null) return NotFound();

        var erroUnidade = await ConferirUnidadeAsync(request.UnidadeId, request.PertenceAUsuario);
        if (erroUnidade is not null) return BadRequest(new { message = erroUnidade });

        if (request.UnidadeId is not null) dispositivo.UnidadeId = request.UnidadeId;

        // Virar ativo do local libera o detentor atual e registra a devolução no histórico —
        // é a única pista de quem estava com o aparelho antes da mudança.
        if (!request.PertenceAUsuario && dispositivo.UsuarioAtualId is not null)
        {
            var nomeUnidade = await _db.Unidades
                .Where(u => u.Id == dispositivo.UnidadeId)
                .Select(u => u.Nome)
                .FirstOrDefaultAsync();

            _db.HistoricoMovimentacoes.Add(new HistoricoMovimentacao
            {
                DispositivoId = dispositivo.Id,
                UsuarioOrigemId = dispositivo.UsuarioAtualId,
                UsuarioDestinoId = null, // destino nulo = sem detentor individual
                Motivo = request.Motivo ?? $"Passou a pertencer a unidade {nomeUnidade}",
                RegistradoPorAdmId = adm.Id,
            });

            dispositivo.UsuarioAtualId = null;
            // Sem detentor o aparelho volta ao estoque, salvo se estiver bloqueado (estado de
            // segurança que só sai pelo comando remoto de desbloqueio).
            if (dispositivo.Status == StatusDispositivo.EmUso)
                dispositivo.Status = StatusDispositivo.EmEstoque;
        }

        dispositivo.PertenceAUsuario = request.PertenceAUsuario;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Tira o dispositivo do inventário mediante a senha do ADM logado, em um de dois modos:
    /// <list type="bullet">
    /// <item>arquivar (padrão) — exclusão lógica, histórico preservado e restaurável;</item>
    /// <item>definitiva — apaga a linha e, em cascata, todo o histórico e os termos assinados.</item>
    /// </list>
    /// </summary>
    [HttpPost("{id:guid}/excluir")]
    public async Task<IActionResult> Excluir(Guid id, ExcluirDispositivoRequest request)
    {
        var validation = await _excluirValidator.ValidateAsync(request);
        if (!validation.IsValid) return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));

        var adm = await _confirmacaoSenha.ConferirAsync(User, request.Senha);
        if (adm is null) return BadRequest(new { message = "Senha incorreta." });

        // A exclusão definitiva também alcança o que já está arquivado — é de lá que a TI costuma
        // apagar de vez, e esses registros estão fora do filtro global.
        var consulta = request.Definitiva ? _db.Dispositivos.IgnoreQueryFilters() : _db.Dispositivos;
        var dispositivo = await consulta.FirstOrDefaultAsync(d => d.Id == id);
        if (dispositivo is null) return NotFound();

        // Aparelho bloqueado não pode sumir da tela: ninguém conseguiria desbloqueá-lo depois.
        if (dispositivo.Status == StatusDispositivo.Bloqueado)
            return BadRequest(new
            {
                message = "Dispositivo bloqueado. Desbloqueie-o antes de excluir, senão ele fica travado sem tela para liberá-lo.",
            });

        if (request.Definitiva)
        {
            _logger.LogWarning(
                "Exclusão DEFINITIVA do dispositivo {DispositivoId} ({NumeroSerie}) pelo ADM {AdmId}. Motivo: {Motivo}",
                dispositivo.Id, dispositivo.NumeroSerie, adm.Id, request.Motivo ?? "(não informado)");

            // Termo de uso é o único dependente com FK Restrict (é prova documental): o EF não o
            // apaga em cascata, e sem removê-lo à mão o DELETE estoura com erro cru de FK.
            var termos = await _db.TermosDeUso.Where(t => t.DispositivoId == id).ToListAsync();
            _db.TermosDeUso.RemoveRange(termos);

            // Os demais dependentes (movimentações, reparos, comentários, alertas, localizações,
            // comandos, geofence) têm ON DELETE CASCADE no banco e saem junto com o dispositivo.
            _db.Dispositivos.Remove(dispositivo);
            await _db.SaveChangesAsync();

            // Só depois de o banco confirmar: apagar o PDF antes deixaria o registro apontando
            // para um arquivo inexistente se o SaveChanges falhasse.
            _arquivos.ExcluirArquivosDoDispositivo(id);

            return NoContent();
        }

        dispositivo.ExcluidoEm = DateTime.UtcNow;
        dispositivo.ExcluidoPorAdmId = adm.Id;
        dispositivo.MotivoExclusao = request.Motivo;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Traz de volta um dispositivo arquivado, com o histórico intacto.</summary>
    [HttpPost("{id:guid}/restaurar")]
    public async Task<IActionResult> Restaurar(Guid id)
    {
        var dispositivo = await _db.Dispositivos.IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.Id == id && d.ExcluidoEm != null);
        if (dispositivo is null) return NotFound();

        dispositivo.ExcluidoEm = null;
        dispositivo.ExcluidoPorAdmId = null;
        dispositivo.MotivoExclusao = null;

        await _db.SaveChangesAsync();
        return NoContent();
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
