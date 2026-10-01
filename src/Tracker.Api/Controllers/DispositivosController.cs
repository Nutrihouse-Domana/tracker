using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tracker.Api.Agente;
using Tracker.Application.Dispositivos;
using Tracker.Application.Dtos;
using Tracker.Application.Seguranca;
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
    private readonly IValidator<DefinirIdentificacaoInternaRequest> _identificacaoInternaValidator;
    private readonly IValidator<AtualizarStatusDispositivoRequest> _statusValidator;
    private readonly IValidator<AtualizarStatusReparoRequest> _statusReparoValidator;
    private readonly IValidator<CriarImpressoraRequest> _impressoraValidator;
    private readonly IValidator<DefinirConexaoImpressoraRequest> _conexaoImpressoraValidator;
    private readonly IValidator<DefinirDadosImpressoraRequest> _dadosImpressoraValidator;
    private readonly IValidator<DefinirAluguelImpressoraRequest> _aluguelImpressoraValidator;
    private readonly ConfirmacaoSenhaAdm _confirmacaoSenha;
    private readonly IArmazenamentoArquivosService _arquivos;
    private readonly IAuditoria _auditoria;
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
        IValidator<DefinirIdentificacaoInternaRequest> identificacaoInternaValidator,
        IValidator<AtualizarStatusDispositivoRequest> statusValidator,
        IValidator<AtualizarStatusReparoRequest> statusReparoValidator,
        IValidator<CriarImpressoraRequest> impressoraValidator,
        IValidator<DefinirConexaoImpressoraRequest> conexaoImpressoraValidator,
        IValidator<DefinirDadosImpressoraRequest> dadosImpressoraValidator,
        IValidator<DefinirAluguelImpressoraRequest> aluguelImpressoraValidator,
        ConfirmacaoSenhaAdm confirmacaoSenha,
        IArmazenamentoArquivosService arquivos,
        IAuditoria auditoria,
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
        _identificacaoInternaValidator = identificacaoInternaValidator;
        _statusValidator = statusValidator;
        _statusReparoValidator = statusReparoValidator;
        _impressoraValidator = impressoraValidator;
        _conexaoImpressoraValidator = conexaoImpressoraValidator;
        _dadosImpressoraValidator = dadosImpressoraValidator;
        _aluguelImpressoraValidator = aluguelImpressoraValidator;
        _confirmacaoSenha = confirmacaoSenha;
        _auditoria = auditoria;
    }

    private const string MensagemDesbloqueieAntes =
        "Dispositivo bloqueado. Desbloqueie o dispositivo pelo comando remoto antes de mudar o status.";

    /// <summary>Id do ADM logado — a autoria vem sempre daqui, nunca do corpo da requisição.</summary>
    private Guid AdmIdAtual() => User.AdmId()!.Value;

    private static DispositivoDto MapToDto(Dispositivo d) => new(
        d.Id,
        d.Tipo,
        d.NumeroSerie,
        d.AssetTag,
        d.IdentificacaoInterna,
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
        // Fabricante/Modelo existem nos com agente (reportados) e na impressora (digitados).
        (d as DispositivoComAgente)?.Fabricante ?? (d as Impressora)?.Fabricante,
        (d as DispositivoComAgente)?.Modelo ?? (d as Impressora)?.Modelo,
        (d as DispositivoComAgente)?.BateriaPercentual,
        (d as DispositivoComAgente)?.NivelProtecao,
        d.UnidadeId,
        d.Unidade?.Nome,
        d.PertenceAUsuario,
        // Ativo do local não tem detentor: quem aparece no lugar do usuário é a unidade.
        d.PertenceAUsuario ? d.UsuarioAtual?.Nome : d.Unidade?.Nome,
        d.ExcluidoEm,
        d.MotivoExclusao,
        d.VencimentoAluguel,
        TipoImpressora: (d as Impressora)?.TipoImpressora,
        Colorida: (d as Impressora)?.Colorida,
        Alugada: (d as Impressora)?.Alugada,
        Conexao: (d as Impressora)?.Conexao,
        EnderecoRede: (d as Impressora)?.EnderecoRede,
        MacAddress: (d as DispositivoComAgente)?.MacAddress ?? (d as Impressora)?.MacAddress,
        MonitorarSnmp: (d as Impressora)?.MonitorarSnmp,
        Online: (d as Impressora)?.Online,
        UltimaLeituraEm: (d as Impressora)?.UltimaLeituraEm,
        ContadorPaginas: (d as Impressora)?.ContadorPaginas);

    /// <summary>Carrega a impressora (tipo concreto, com discriminador) pelo id; nulo nos demais tipos.</summary>
    private Task<Impressora?> ObterImpressoraAsync(Guid id) =>
        _db.Set<Impressora>().FirstOrDefaultAsync(i => i.Id == id);

    /// <summary>Dispositivos ativos. Os arquivados ficam de fora (filtro global) — veja /arquivados.</summary>
    [HttpGet]
    [Authorize(Policy = Permissoes.DispositivosVer)]
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
    [Authorize(Policy = Permissoes.DispositivosVer)]
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
    [Authorize(Policy = Permissoes.DispositivosVer)]
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
    [Authorize(Policy = Permissoes.DispositivosCadastrar)]
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
        _auditoria.Registrar(AcaoAuditoria.DispositivoCriado,
            $"Cadastrou o notebook {notebook.NumeroSerie} ({notebook.AssetTag}).", "Dispositivo", notebook.Id);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(ObterPorId), new { id = notebook.Id }, MapToDto(notebook));
    }

    /// <summary>Cadastra um celular ou tablet. Rota antiga /telefones mantida como alias.</summary>
    [HttpPost("mobiles")]
    [HttpPost("telefones")]
    [Authorize(Policy = Permissoes.DispositivosCadastrar)]
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
        _auditoria.Registrar(AcaoAuditoria.DispositivoCriado,
            $"Cadastrou o mobile {mobile.NumeroSerie} ({mobile.AssetTag}).", "Dispositivo", mobile.Id);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(ObterPorId), new { id = mobile.Id }, MapToDto(mobile));
    }

    [HttpPost("perifericos")]
    [Authorize(Policy = Permissoes.DispositivosCadastrar)]
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
        _auditoria.Registrar(AcaoAuditoria.DispositivoCriado,
            $"Cadastrou o periférico {periferico.NumeroSerie} ({periferico.TipoPeriferico}).", "Dispositivo", periferico.Id);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(ObterPorId), new { id = periferico.Id }, MapToDto(periferico));
    }

    /// <summary>
    /// Cadastra uma impressora. Sem agente e sempre do LOCAL: nasce com <c>PertenceAUsuario = false</c>
    /// e exige unidade. O aluguel é o interruptor <c>Alugada</c>, não o prefixo "MAQ".
    /// </summary>
    [HttpPost("impressoras")]
    [Authorize(Policy = Permissoes.DispositivosCadastrar)]
    public async Task<ActionResult<DispositivoDto>> CriarImpressora(CriarImpressoraRequest request)
    {
        var validation = await _impressoraValidator.ValidateAsync(request);
        if (!validation.IsValid) return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));

        // Patrimônio em branco é "sem patrimônio" (nulo): string vazia colidiria no índice único.
        var assetTag = string.IsNullOrWhiteSpace(request.AssetTag) ? null : request.AssetTag.Trim();

        var duplicidade = await ConferirDuplicidadeAsync(request.NumeroSerie, assetTag);
        if (duplicidade is not null) return Conflict(new { message = duplicidade });

        var erroUnidade = await ConferirUnidadeAsync(request.UnidadeId, pertenceAUsuario: false);
        if (erroUnidade is not null) return BadRequest(new { message = erroUnidade });

        var impressora = new Impressora
        {
            UnidadeId = request.UnidadeId,
            NumeroSerie = request.NumeroSerie,
            AssetTag = assetTag,
            TipoImpressora = request.TipoImpressora,
            Fabricante = string.IsNullOrWhiteSpace(request.Fabricante) ? null : request.Fabricante.Trim(),
            Modelo = string.IsNullOrWhiteSpace(request.Modelo) ? null : request.Modelo.Trim(),
            Colorida = request.Colorida,
            Alugada = request.Alugada,
            VencimentoAluguel = request.VencimentoAluguel,
            DataAquisicao = request.DataAquisicao,
            Observacoes = request.Observacoes,
        };
        RegrasDispositivo.AplicarConexao(
            impressora, request.Conexao, request.EnderecoRede, request.MacAddress, request.MonitorarSnmp);

        _db.Dispositivos.Add(impressora);
        _auditoria.Registrar(AcaoAuditoria.DispositivoCriado,
            $"Cadastrou a impressora {impressora.NumeroSerie}"
            + $"{(impressora.Modelo is null ? "" : $" ({impressora.Modelo})")}"
            + $"{(impressora.Alugada ? ", alugada" : "")}.",
            "Dispositivo", impressora.Id);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(ObterPorId), new { id = impressora.Id }, MapToDto(impressora));
    }

    [HttpPut("{id:guid}/status")]
    [Authorize(Policy = Permissoes.DispositivosMovimentar)]
    public async Task<IActionResult> AtualizarStatus(Guid id, AtualizarStatusDispositivoRequest request)
    {
        var validation = await _statusValidator.ValidateAsync(request);
        if (!validation.IsValid) return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));

        // Bloqueado é efeito do comando remoto confirmado pelo agente, não rótulo manual: marcado à
        // mão, a tela diria "bloqueado" com o aparelho destravado nas mãos do usuário.
        if (request.Status == StatusDispositivo.Bloqueado)
            return BadRequest(new { message = "Para bloquear o dispositivo, use o comando remoto de bloqueio." });

        var dispositivo = await _db.Dispositivos.FindAsync(id);
        if (dispositivo is null) return NotFound();

        // E o inverso: tirar de Bloqueado à mão deixaria o aparelho travado sem comando de
        // desbloqueio a caminho (o agente reaplica a trava sozinho). A exceção é a BAIXA: é o
        // destino de um aparelho roubado/perdido que nunca mais confirma o Desbloquear. Ela é
        // segura porque o estado de trava que o heartbeat manda vem do último comando
        // (RegrasDispositivo.DeveEstarBloqueado), não do status — dar baixa não destrava nada.
        if (dispositivo.Status == StatusDispositivo.Bloqueado && request.Status != StatusDispositivo.Baixado)
            return BadRequest(new { message = MensagemDesbloqueieAntes });

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
                    RegistradoPorAdmId = AdmIdAtual(),
                });
            }
            dispositivo.UsuarioAtualId = null;
        }

        _auditoria.Registrar(AcaoAuditoria.DispositivoStatusAlterado,
            $"Alterou o status de {dispositivo.NumeroSerie} para {request.Status}.", "Dispositivo", dispositivo.Id);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Atribui o dispositivo a um usuário, registrando a movimentação (origem = posse/local anteriores).</summary>
    [HttpPost("{id:guid}/atribuir")]
    [Authorize(Policy = Permissoes.DispositivosMovimentar)]
    public async Task<ActionResult<HistoricoMovimentacaoDto>> Atribuir(Guid id, AtribuirDispositivoRequest request)
    {
        var validation = await _atribuirValidator.ValidateAsync(request);
        if (!validation.IsValid) return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));

        var dispositivo = await _db.Dispositivos.FirstOrDefaultAsync(d => d.Id == id);
        if (dispositivo is null) return NotFound(new { message = "Dispositivo não encontrado." });

        // Impressora é sempre do local — antes da regra genérica, para a mensagem não sugerir
        // ligar "Pertence a usuário" (o que também é recusado para ela).
        if (!RegrasDispositivo.AceitaDetentor(dispositivo))
            return BadRequest(new { message = RegrasDispositivo.MensagemImpressoraSemDetentor });

        // Ativo do local não tem detentor individual: atribuir aqui contradiria o interruptor.
        if (!dispositivo.PertenceAUsuario)
            return BadRequest(new
            {
                message = "Este dispositivo pertence à unidade, não a um usuário. "
                          + "Ative \"Pertence a usuário\" antes de atribuí-lo.",
            });

        var usuarioDestino = await _db.Usuarios.FindAsync(request.UsuarioId);
        if (usuarioDestino is null) return NotFound(new { message = "Usuário destino não encontrado." });

        // Detentor desativado saiu da empresa/da lista: entregar-lhe um ativo criaria posse que
        // ninguém acompanha.
        if (!usuarioDestino.Ativo)
            return BadRequest(new { message = "Este usuário está desativado. Reative-o antes de atribuir um dispositivo." });

        // Sem esta checagem, um id inexistente só estouraria no SaveChanges como erro cru de FK (500).
        if (request.LocalDestinoId is { } localId && !await _db.Locais.AnyAsync(l => l.Id == localId))
            return BadRequest(new { message = "Local de destino não encontrado." });

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
            RegistradoPorAdmId = AdmIdAtual(),
        };

        dispositivo.UsuarioAtualId = usuarioDestino.Id;
        // Ao ganhar dono, passa a "Em uso" — exceto se estiver bloqueado, estado de segurança
        // que só sai pelo comando remoto de desbloqueio.
        if (dispositivo.Status != StatusDispositivo.Bloqueado)
            dispositivo.Status = StatusDispositivo.EmUso;

        _db.HistoricoMovimentacoes.Add(movimentacao);
        _auditoria.Registrar(AcaoAuditoria.DispositivoAtribuido,
            $"Atribuiu {dispositivo.NumeroSerie} a {usuarioDestino.Nome}.", "Dispositivo", dispositivo.Id,
            request.Motivo);
        await _db.SaveChangesAsync();

        var dto = new HistoricoMovimentacaoDto(
            movimentacao.Id, movimentacao.DispositivoId,
            movimentacao.UsuarioOrigemId, null,
            movimentacao.UsuarioDestinoId, usuarioDestino.Nome,
            movimentacao.LocalOrigemId, null,
            movimentacao.LocalDestinoId, null,
            movimentacao.DataMovimentacao, movimentacao.Motivo,
            User.AdmNome());

        return Ok(dto);
    }

    /// <summary>Define (ou limpa) a unidade que o dispositivo atende.</summary>
    [HttpPut("{id:guid}/unidade")]
    [Authorize(Policy = Permissoes.DispositivosCadastrar)]
    public async Task<IActionResult> DefinirUnidade(Guid id, DefinirUnidadeDispositivoRequest request)
    {
        var dispositivo = await _db.Dispositivos.FirstOrDefaultAsync(d => d.Id == id);
        if (dispositivo is null) return NotFound();

        var erroUnidade = await ConferirUnidadeAsync(request.UnidadeId, dispositivo.PertenceAUsuario);
        if (erroUnidade is not null) return BadRequest(new { message = erroUnidade });

        dispositivo.UnidadeId = request.UnidadeId;
        _auditoria.Registrar(AcaoAuditoria.DispositivoUnidadeDefinida,
            $"Definiu a unidade de {dispositivo.NumeroSerie}.", "Dispositivo", dispositivo.Id);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Define (ou limpa) a identificação interna do dispositivo: um rótulo próprio da TI, à parte do
    /// hostname que o agente reporta. Exige a senha do ADM logado — passa a ser o nome exibido do
    /// ativo nas telas. Enviar vazio limpa o campo e volta a exibir o hostname/número de série.
    /// </summary>
    [HttpPut("{id:guid}/identificacao-interna")]
    [Authorize(Policy = Permissoes.DispositivosCadastrar)]
    public async Task<IActionResult> DefinirIdentificacaoInterna(Guid id, DefinirIdentificacaoInternaRequest request)
    {
        var validation = await _identificacaoInternaValidator.ValidateAsync(request);
        if (!validation.IsValid) return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));

        var adm = await _confirmacaoSenha.ConferirAsync(User, request.Senha);
        // 400, e não 401: o 401 faria o site tratar como sessão expirada e deslogar o ADM.
        if (adm is null) return BadRequest(new { message = "Senha incorreta." });

        var dispositivo = await _db.Dispositivos.FirstOrDefaultAsync(d => d.Id == id);
        if (dispositivo is null) return NotFound();

        // Campo em branco significa "sem identificação interna", não string vazia no banco: assim o
        // fallback para hostname/número de série volta a valer.
        var valor = string.IsNullOrWhiteSpace(request.IdentificacaoInterna)
            ? null
            : request.IdentificacaoInterna.Trim();
        dispositivo.IdentificacaoInterna = valor;

        _auditoria.Registrar(AcaoAuditoria.DispositivoIdentificacaoInternaDefinida,
            valor is null
                ? $"Removeu a identificação interna de {dispositivo.NumeroSerie}."
                : $"Definiu a identificação interna de {dispositivo.NumeroSerie} como \"{valor}\".",
            "Dispositivo", dispositivo.Id, request.Motivo);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Define ou limpa (enviando nulo) o vencimento do contrato de aluguel do equipamento.
    /// Não pede a senha do ADM: é dado operacional do contrato, não mudança de responsabilidade
    /// sobre o ativo — mas continua auditado, como toda escrita.
    /// </summary>
    [HttpPut("{id:guid}/vencimento-aluguel")]
    [Authorize(Policy = Permissoes.DispositivosCadastrar)]
    public async Task<IActionResult> DefinirVencimentoAluguel(Guid id, DefinirVencimentoAluguelRequest request)
    {
        var dispositivo = await _db.Dispositivos.FirstOrDefaultAsync(d => d.Id == id);
        if (dispositivo is null) return NotFound();

        // DateOnly: sem hora e sem fuso. Com DateTime, o conversor de JSON carimbava 'Z' e o
        // navegador em UTC-3 exibia o dia anterior.
        var valor = request.VencimentoAluguel;
        dispositivo.VencimentoAluguel = valor;

        _auditoria.Registrar(AcaoAuditoria.DispositivoVencimentoAluguelDefinido,
            valor is null
                ? $"Removeu o vencimento de aluguel de {dispositivo.NumeroSerie}."
                : $"Definiu o vencimento de aluguel de {dispositivo.NumeroSerie} para {valor:dd/MM/yyyy}.",
            "Dispositivo", dispositivo.Id, request.Motivo);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Liga/desliga o interruptor de aluguel da IMPRESSORA (nos outros tipos o aluguel é o prefixo
    /// "MAQ" da identificação, e este endpoint recusa com 400). Sem senha do ADM, como o vencimento:
    /// é dado do contrato. Auditado.
    /// </summary>
    [HttpPut("{id:guid}/aluguel")]
    [Authorize(Policy = Permissoes.DispositivosCadastrar)]
    public async Task<IActionResult> DefinirAluguel(Guid id, DefinirAluguelImpressoraRequest request)
    {
        var validation = await _aluguelImpressoraValidator.ValidateAsync(request);
        if (!validation.IsValid) return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));

        var dispositivo = await _db.Dispositivos.FirstOrDefaultAsync(d => d.Id == id);
        if (dispositivo is null) return NotFound();
        if (dispositivo is not Impressora impressora)
            return BadRequest(new
            {
                message = "O interruptor de aluguel é só das impressoras. Nos demais tipos, o alugado é "
                          + "marcado pelo prefixo \"MAQ\" na identificação.",
            });

        impressora.Alugada = request.Alugada;
        _auditoria.Registrar(AcaoAuditoria.DispositivoAluguelDefinido,
            request.Alugada
                ? $"Marcou a impressora {impressora.NumeroSerie} como alugada."
                : $"Marcou a impressora {impressora.NumeroSerie} como própria.",
            "Dispositivo", impressora.Id, request.Motivo);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Define como a impressora está ligada (rede/USB), endereço, MAC e se o servidor a monitora por
    /// SNMP. Com USB o endereço é descartado e o SNMP desligado. Sem senha do ADM; auditado.
    /// </summary>
    [HttpPut("{id:guid}/conexao-impressora")]
    [Authorize(Policy = Permissoes.DispositivosCadastrar)]
    public async Task<IActionResult> DefinirConexaoImpressora(Guid id, DefinirConexaoImpressoraRequest request)
    {
        var validation = await _conexaoImpressoraValidator.ValidateAsync(request);
        if (!validation.IsValid) return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));

        var dispositivo = await _db.Dispositivos.FirstOrDefaultAsync(d => d.Id == id);
        if (dispositivo is null) return NotFound();
        if (dispositivo is not Impressora impressora)
            return BadRequest(new { message = "Conexão de rede/USB só se aplica a impressoras." });

        RegrasDispositivo.AplicarConexao(
            impressora, request.Conexao, request.EnderecoRede, request.MacAddress, request.MonitorarSnmp);

        var descricao = impressora.Conexao == ConexaoImpressora.Usb
            ? "USB"
            : $"rede ({impressora.EnderecoRede ?? "sem endereço"}), SNMP {(impressora.MonitorarSnmp ? "ligado" : "desligado")}";
        _auditoria.Registrar(AcaoAuditoria.ImpressoraConexaoDefinida,
            $"Definiu a conexão da impressora {impressora.NumeroSerie}: {descricao}.",
            "Dispositivo", impressora.Id);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Corrige os dados descritivos da impressora (tipo, fabricante, modelo, colorida). Sem senha do
    /// ADM; auditado como <see cref="AcaoAuditoria.ImpressoraDadosDefinidos"/>.
    /// </summary>
    [HttpPut("{id:guid}/dados-impressora")]
    [Authorize(Policy = Permissoes.DispositivosCadastrar)]
    public async Task<IActionResult> DefinirDadosImpressora(Guid id, DefinirDadosImpressoraRequest request)
    {
        var validation = await _dadosImpressoraValidator.ValidateAsync(request);
        if (!validation.IsValid) return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));

        var impressora = await ObterImpressoraAsync(id);
        if (impressora is null)
            return await _db.Dispositivos.AnyAsync(d => d.Id == id)
                ? BadRequest(new { message = "Estes dados só se aplicam a impressoras." })
                : NotFound();

        impressora.TipoImpressora = request.TipoImpressora;
        impressora.Fabricante = string.IsNullOrWhiteSpace(request.Fabricante) ? null : request.Fabricante.Trim();
        impressora.Modelo = string.IsNullOrWhiteSpace(request.Modelo) ? null : request.Modelo.Trim();
        impressora.Colorida = request.Colorida;

        _auditoria.Registrar(AcaoAuditoria.ImpressoraDadosDefinidos,
            $"Alterou os dados da impressora {impressora.NumeroSerie}: {impressora.TipoImpressora}, "
            + $"{impressora.Fabricante ?? "sem fabricante"} {impressora.Modelo ?? ""}".TrimEnd()
            + $", {(impressora.Colorida ? "colorida" : "monocromática")}.",
            "Dispositivo", impressora.Id);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Alterna entre "pertence a um usuário" e "pertence ao local (unidade)". Exige a senha do
    /// ADM logado: desligar o interruptor solta o detentor atual do ativo.
    /// </summary>
    [HttpPut("{id:guid}/posse")]
    [Authorize(Policy = Permissoes.DispositivosMovimentar)]
    public async Task<IActionResult> DefinirPosse(Guid id, DefinirPosseDispositivoRequest request)
    {
        var validation = await _posseValidator.ValidateAsync(request);
        if (!validation.IsValid) return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));

        var dispositivo = await _db.Dispositivos.FirstOrDefaultAsync(d => d.Id == id);
        if (dispositivo is null) return NotFound();

        // Antes da senha: é recusa estrutural, pedir a senha para depois recusar só confundiria.
        if (request.PertenceAUsuario && !RegrasDispositivo.AceitaDetentor(dispositivo))
            return BadRequest(new { message = RegrasDispositivo.MensagemImpressoraSemDetentor });

        var adm = await _confirmacaoSenha.ConferirAsync(User, request.Senha);
        // 400, e não 401: o 401 faria o site tratar como sessão expirada e deslogar o ADM.
        if (adm is null) return BadRequest(new { message = "Senha incorreta." });

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

        // "Em uso" precisa de responsável na posse NOVA: de local para usuário o aparelho ainda
        // não tem detentor (a atribuição vem depois), e de usuário para local sem unidade não há
        // quem responda. Nesses casos volta ao estoque; Bloqueado e os demais ficam como estão.
        dispositivo.Status = RegrasDispositivo.StatusCoerenteComResponsavel(dispositivo);

        _auditoria.Registrar(AcaoAuditoria.DispositivoPosseAlterada,
            $"Alterou a posse de {dispositivo.NumeroSerie} para "
            + $"{(request.PertenceAUsuario ? "usuário nominal" : "ativo do local")}.",
            "Dispositivo", dispositivo.Id, request.Motivo);
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
    [Authorize(Policy = Permissoes.DispositivosExcluir)]
    public async Task<IActionResult> Excluir(Guid id, ExcluirDispositivoRequest request)
    {
        var validation = await _excluirValidator.ValidateAsync(request);
        if (!validation.IsValid) return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));

        // Arquivar exige "excluir"; apagar de vez exige, ADEMAIS, "excluir definitivo". O endpoint é
        // um só (o modo vem no corpo), então a segunda permissão é conferida aqui dentro.
        if (request.Definitiva && !User.HasClaim(Permissoes.TipoClaim, Permissoes.DispositivosExcluirDefinitivo))
            return Forbid();

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

        // Mesmo risco antes da confirmação: o status só vira Bloqueado quando o agente confirma,
        // mas um Bloquear já emitido (e não seguido de Desbloquear) trava o aparelho assim que
        // chegar — e aí não haveria mais tela para liberá-lo.
        if (RegrasDispositivo.TemBloqueioAChegar(await _db.UltimoComandoVigenteAsync(dispositivo.Id)))
            return BadRequest(new
            {
                message = "Há um comando de bloqueio pendente para este dispositivo. Envie o desbloqueio "
                          + "(ou aguarde e desbloqueie) antes de excluir, senão ele fica travado sem tela para liberá-lo.",
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

            // Auditoria ANTES do Remove: LogAuditoria não tem FK para Dispositivo (EntidadeId é
            // Guid solto), então o registro sobrevive ao apagamento do alvo em cascata.
            _auditoria.Registrar(AcaoAuditoria.DispositivoExcluidoDefinitivo,
                $"Excluiu DEFINITIVAMENTE o dispositivo {dispositivo.NumeroSerie}.", "Dispositivo", dispositivo.Id,
                request.Motivo);

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

        _auditoria.Registrar(AcaoAuditoria.DispositivoArquivado,
            $"Arquivou o dispositivo {dispositivo.NumeroSerie}.", "Dispositivo", dispositivo.Id, request.Motivo);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Traz de volta um dispositivo arquivado, com o histórico intacto.</summary>
    [HttpPost("{id:guid}/restaurar")]
    [Authorize(Policy = Permissoes.DispositivosExcluir)]
    public async Task<IActionResult> Restaurar(Guid id)
    {
        var dispositivo = await _db.Dispositivos.IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.Id == id && d.ExcluidoEm != null);
        if (dispositivo is null) return NotFound();

        dispositivo.ExcluidoEm = null;
        dispositivo.ExcluidoPorAdmId = null;
        dispositivo.MotivoExclusao = null;

        _auditoria.Registrar(AcaoAuditoria.DispositivoRestaurado,
            $"Restaurou o dispositivo {dispositivo.NumeroSerie}.", "Dispositivo", dispositivo.Id);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("{id:guid}/historico-movimentacao")]
    [Authorize(Policy = Permissoes.DispositivosVer)]
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
                h.DataMovimentacao, h.Motivo,
                _db.Users.Where(u => u.Id == h.RegistradoPorAdmId).Select(u => u.Nome).FirstOrDefault()))
            .ToListAsync();

        return Ok(historico);
    }

    [HttpGet("{id:guid}/historico-localizacao")]
    [Authorize(Policy = Permissoes.DispositivosVer)]
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
    [Authorize(Policy = Permissoes.DispositivosMovimentar)]
    public async Task<ActionResult<HistoricoReparoDto>> CriarReparo(Guid id, CriarReparoRequest request)
    {
        var validation = await _reparoValidator.ValidateAsync(request);
        if (!validation.IsValid) return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));

        var dispositivo = await _db.Dispositivos.FirstOrDefaultAsync(d => d.Id == id);
        if (dispositivo is null) return NotFound(new { message = "Dispositivo não encontrado." });

        // Abrir reparo põe o aparelho "Em manutenção" — de um Bloqueado, apagaria o estado de
        // segurança sem que nenhum comando de desbloqueio tivesse chegado ao agente.
        if (dispositivo.Status == StatusDispositivo.Bloqueado)
            return BadRequest(new { message = MensagemDesbloqueieAntes });

        var reparo = new HistoricoReparo
        {
            DispositivoId = id,
            Descricao = request.Descricao,
            CustoEstimado = request.CustoEstimado,
            RegistradoPorAdmId = AdmIdAtual(),
        };

        _db.HistoricoReparos.Add(reparo);
        dispositivo.Status = StatusDispositivo.EmManutencao;

        _auditoria.Registrar(AcaoAuditoria.ReparoAberto,
            $"Abriu um reparo em {dispositivo.NumeroSerie}.", "Dispositivo", dispositivo.Id, request.Descricao);
        await _db.SaveChangesAsync();

        return Ok(new HistoricoReparoDto(
            reparo.Id, reparo.DispositivoId, reparo.Descricao, reparo.Status,
            reparo.CustoEstimado, reparo.DataAbertura, reparo.DataConclusao, reparo.RegistradoPorAdmId,
            User.AdmNome()));
    }

    [HttpGet("{id:guid}/reparos")]
    [Authorize(Policy = Permissoes.DispositivosVer)]
    public async Task<ActionResult<IEnumerable<HistoricoReparoDto>>> ListarReparos(Guid id)
    {
        var reparos = await _db.HistoricoReparos.AsNoTracking()
            .Where(r => r.DispositivoId == id)
            .OrderByDescending(r => r.DataAbertura)
            .Select(r => new HistoricoReparoDto(
                r.Id, r.DispositivoId, r.Descricao, r.Status,
                r.CustoEstimado, r.DataAbertura, r.DataConclusao, r.RegistradoPorAdmId,
                _db.Users.Where(u => u.Id == r.RegistradoPorAdmId).Select(u => u.Nome).FirstOrDefault()))
            .ToListAsync();

        return Ok(reparos);
    }

    [HttpPut("{id:guid}/reparos/{reparoId:guid}/status")]
    [Authorize(Policy = Permissoes.DispositivosMovimentar)]
    public async Task<IActionResult> AtualizarStatusReparo(Guid id, Guid reparoId, AtualizarStatusReparoRequest request)
    {
        var validation = await _statusReparoValidator.ValidateAsync(request);
        if (!validation.IsValid) return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));

        var reparo = await _db.HistoricoReparos.FirstOrDefaultAsync(r => r.Id == reparoId && r.DispositivoId == id);
        if (reparo is null) return NotFound();

        if (request.Status == StatusReparo.Concluido)
        {
            var dispositivo = await _db.Dispositivos.FirstAsync(d => d.Id == id);
            // Concluir o reparo recalcula o status do aparelho; num Bloqueado isso o destravaria
            // na tela sem desbloqueio de verdade no agente.
            if (dispositivo.Status == StatusDispositivo.Bloqueado)
                return BadRequest(new { message = MensagemDesbloqueieAntes });

            dispositivo.Status = dispositivo.UsuarioAtualId is null ? StatusDispositivo.EmEstoque : StatusDispositivo.EmUso;
        }

        reparo.Status = request.Status;
        if (request.Status is StatusReparo.Concluido or StatusReparo.Cancelado)
            reparo.DataConclusao = DateTime.UtcNow;

        _auditoria.Registrar(AcaoAuditoria.ReparoStatusAlterado,
            $"Alterou o status de um reparo para {request.Status}.", "Dispositivo", id);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id:guid}/comentarios")]
    [Authorize(Policy = Permissoes.DispositivosMovimentar)]
    public async Task<ActionResult<ComentarioDispositivoDto>> CriarComentario(Guid id, CriarComentarioRequest request)
    {
        var validation = await _comentarioValidator.ValidateAsync(request);
        if (!validation.IsValid) return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));

        var dispositivoExiste = await _db.Dispositivos.AnyAsync(d => d.Id == id);
        if (!dispositivoExiste) return NotFound(new { message = "Dispositivo não encontrado." });

        // Autoria pelo usuário logado, nunca por id vindo do corpo (que seria forjável).
        var admId = AdmIdAtual();
        var admNome = User.AdmNome();

        var comentario = new ComentarioDispositivo
        {
            DispositivoId = id,
            Texto = request.Texto,
            CriadoPorAdmId = admId,
        };

        _db.ComentariosDispositivo.Add(comentario);
        _auditoria.Registrar(AcaoAuditoria.ComentarioAdicionado,
            "Adicionou um comentário ao dispositivo.", "Dispositivo", id);
        await _db.SaveChangesAsync();

        return Ok(new ComentarioDispositivoDto(
            comentario.Id, comentario.DispositivoId, comentario.Texto,
            comentario.CriadoPorAdmId, admNome, comentario.CriadoEm));
    }

    [HttpGet("{id:guid}/comentarios")]
    [Authorize(Policy = Permissoes.DispositivosVer)]
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
