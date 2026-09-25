using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tracker.Api.Seguranca;
using Tracker.Application.Dispositivos;
using Tracker.Application.Dtos;
using Tracker.Application.Seguranca;
using Tracker.Domain.Entities;
using Tracker.Domain.Enums;
using Tracker.Infrastructure.Data;
using Tracker.Infrastructure.Pdf;
using Tracker.Infrastructure.Storage;

namespace Tracker.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TermosDeUsoController : ControllerBase
{
    private readonly TrackerDbContext _db;
    private readonly ITermoDeUsoPdfService _pdfService;
    private readonly IArmazenamentoArquivosService _armazenamento;
    private readonly IValidator<GerarTermoDeUsoRequest> _gerarValidator;
    private readonly IAuditoria _auditoria;

    public TermosDeUsoController(
        TrackerDbContext db,
        ITermoDeUsoPdfService pdfService,
        IArmazenamentoArquivosService armazenamento,
        IValidator<GerarTermoDeUsoRequest> gerarValidator,
        IAuditoria auditoria)
    {
        _db = db;
        _pdfService = pdfService;
        _armazenamento = armazenamento;
        _gerarValidator = gerarValidator;
        _auditoria = auditoria;
    }

    /// <summary>Gera o PDF pré-preenchido para impressão. Não persiste nada — o registro só é
    /// criado quando o termo assinado (escaneado) é enviado via <see cref="Upload"/>.</summary>
    [HttpPost("gerar-pdf")]
    [Authorize(Policy = Permissoes.DispositivosMovimentar)]
    public async Task<IActionResult> GerarPdf(GerarTermoDeUsoRequest request)
    {
        var validation = await _gerarValidator.ValidateAsync(request);
        if (!validation.IsValid) return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));

        var dispositivo = await _db.Dispositivos.AsNoTracking().FirstOrDefaultAsync(d => d.Id == request.DispositivoId);
        if (dispositivo is null) return NotFound(new { message = "Dispositivo não encontrado." });
        // Termo de uso é de detentor nominal; impressora é sempre do local.
        if (!RegrasDispositivo.AceitaDetentor(dispositivo))
            return BadRequest(new { message = RegrasDispositivo.MensagemImpressoraSemDetentor });

        var usuario = await _db.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Id == request.UsuarioId);
        if (usuario is null) return NotFound(new { message = "Usuário não encontrado." });

        var dados = new DadosTermoDeUso(
            request.VersaoTermo,
            usuario.Nome,
            FormatarCpf(usuario.Cpf),
            usuario.Email,
            usuario.Telefone,
            dispositivo.Tipo.ToString(),
            dispositivo.NumeroSerie,
            dispositivo.AssetTag,
            (dispositivo as DispositivoComAgente)?.Hostname,
            request.EstadoDispositivo ?? string.Empty,
            DateTime.UtcNow);

        var pdfBytes = _pdfService.Gerar(dados);
        var nomeArquivo = $"termo-uso-{dispositivo.NumeroSerie}.pdf";
        return File(pdfBytes, "application/pdf", nomeArquivo);
    }

    /// <summary>Recebe o termo assinado escaneado, calcula o hash e cria o registro do TermoDeUso.</summary>
    [HttpPost]
    [Authorize(Policy = Permissoes.DispositivosMovimentar)]
    [RequestSizeLimit(20_000_000)]
    public async Task<ActionResult<TermoDeUsoDto>> Upload(
        [FromForm] Guid dispositivoId,
        [FromForm] Guid usuarioId,
        [FromForm] string versaoTermo,
        IFormFile arquivo)
    {
        if (arquivo is null || arquivo.Length == 0)
            return BadRequest(new { message = "Nenhum arquivo enviado." });

        if (!string.Equals(Path.GetExtension(arquivo.FileName), ".pdf", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Apenas arquivos PDF são aceitos." });

        var tipoDispositivo = await _db.Dispositivos
            .Where(d => d.Id == dispositivoId)
            .Select(d => (TipoDispositivo?)d.Tipo)
            .FirstOrDefaultAsync();
        if (tipoDispositivo is null) return NotFound(new { message = "Dispositivo não encontrado." });
        if (tipoDispositivo == TipoDispositivo.Impressora)
            return BadRequest(new { message = RegrasDispositivo.MensagemImpressoraSemDetentor });

        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId);
        if (usuario is null) return NotFound(new { message = "Usuário não encontrado." });

        await using var stream = arquivo.OpenReadStream();
        var (caminhoRelativo, hash) = await _armazenamento.SalvarTermoAssinadoAsync(dispositivoId, stream, arquivo.FileName);

        var termo = new TermoDeUso
        {
            DispositivoId = dispositivoId,
            UsuarioId = usuarioId,
            VersaoTermo = versaoTermo,
            HashDocumento = hash,
            CaminhoArquivoAnexado = caminhoRelativo,
            // Autoria pelo ADM logado, nunca por id vindo do formulário.
            RegistradoPorAdmId = User.AdmId()!.Value,
        };

        _db.TermosDeUso.Add(termo);
        _auditoria.Registrar(AcaoAuditoria.TermoRegistrado,
            $"Registrou o termo de uso de {usuario.Nome}.", "Dispositivo", dispositivoId);
        await _db.SaveChangesAsync();

        return Ok(new TermoDeUsoDto(
            termo.Id, termo.DispositivoId, termo.UsuarioId, usuario.Nome,
            termo.VersaoTermo, termo.DataAceite, termo.HashDocumento,
            termo.CaminhoArquivoAnexado, AssinadoAnexado: true));
    }

    [HttpGet("dispositivo/{dispositivoId:guid}")]
    [Authorize(Policy = Permissoes.DispositivosVer)]
    public async Task<ActionResult<IEnumerable<TermoDeUsoDto>>> ListarPorDispositivo(Guid dispositivoId)
    {
        var termos = await _db.TermosDeUso.AsNoTracking()
            .Include(t => t.Usuario)
            .Where(t => t.DispositivoId == dispositivoId)
            .OrderByDescending(t => t.DataAceite)
            .Select(t => new TermoDeUsoDto(
                t.Id, t.DispositivoId, t.UsuarioId, t.Usuario!.Nome,
                t.VersaoTermo, t.DataAceite, t.HashDocumento, t.CaminhoArquivoAnexado, true))
            .ToListAsync();

        return Ok(termos);
    }

    [HttpGet("{id:guid}/arquivo")]
    [Authorize(Policy = Permissoes.DispositivosVer)]
    public async Task<IActionResult> BaixarArquivo(Guid id)
    {
        var termo = await _db.TermosDeUso.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
        if (termo is null) return NotFound();

        var stream = await _armazenamento.AbrirAsync(termo.CaminhoArquivoAnexado);
        return File(stream, "application/pdf", $"termo-assinado-{termo.Id}.pdf");
    }

    /// <summary>Igual a /arquivo, mas sem Content-Disposition: attachment — permite abrir o PDF
    /// direto no navegador (ex.: em um &lt;iframe&gt;) em vez de forçar o download.</summary>
    [HttpGet("{id:guid}/visualizar")]
    [Authorize(Policy = Permissoes.DispositivosVer)]
    public async Task<IActionResult> Visualizar(Guid id)
    {
        var termo = await _db.TermosDeUso.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
        if (termo is null) return NotFound();

        var stream = await _armazenamento.AbrirAsync(termo.CaminhoArquivoAnexado);
        return File(stream, "application/pdf");
    }

    /// <summary>Formata o CPF completo (XXX.XXX.XXX-XX) para o termo de responsabilidade.</summary>
    private static string FormatarCpf(string cpf)
    {
        var digitos = new string(cpf.Where(char.IsDigit).ToArray());
        return digitos.Length == 11
            ? $"{digitos[..3]}.{digitos[3..6]}.{digitos[6..9]}-{digitos[9..]}"
            : cpf;
    }
}
