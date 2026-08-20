using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tracker.Api.Seguranca;
using Tracker.Application.Dtos;
using Tracker.Application.Seguranca;
using Tracker.Domain.Entities;
using Tracker.Domain.Enums;
using Tracker.Infrastructure.Data;
using Tracker.Infrastructure.Storage;

namespace Tracker.Api.Controllers;

/// <summary>
/// Gestão do pacote OTA do agente Android: a TI publica um APK novo aqui e a frota se atualiza
/// sozinha (oferta no heartbeat + download autenticado), sem loja e sem tocar nos aparelhos.
/// </summary>
[ApiController]
[Route("api/agente-android")]
[Authorize(Policy = Permissoes.AgentePublicar)]
public class AgenteAndroidController : ControllerBase
{
    private readonly TrackerDbContext _db;
    private readonly IArmazenamentoPacotesAgenteService _pacotes;
    private readonly ConfirmacaoSenhaAdm _confirmacaoSenha;
    private readonly IAuditoria _auditoria;

    public AgenteAndroidController(
        TrackerDbContext db,
        IArmazenamentoPacotesAgenteService pacotes,
        ConfirmacaoSenhaAdm confirmacaoSenha,
        IAuditoria auditoria)
    {
        _db = db;
        _pacotes = pacotes;
        _confirmacaoSenha = confirmacaoSenha;
        _auditoria = auditoria;
    }

    /// <summary>Pacote vigente, histórico de versões e a situação da frota Android.</summary>
    [HttpGet("pacotes")]
    public async Task<ActionResult<SituacaoAgenteAndroidDto>> Situacao()
    {
        var historico = await _db.PacotesAgenteAndroid.AsNoTracking()
            .OrderByDescending(p => p.VersaoCode)
            .Select(p => new PacoteAgenteAndroidDto(
                p.Id, p.VersaoCode, p.VersaoNome, p.NomeArquivoOriginal, p.HashSha256, p.TamanhoBytes, p.CriadoEm))
            .ToListAsync();

        var vigente = historico.FirstOrDefault();

        // Frota Android = mobiles que já falaram com o servidor. Sem versionCode reportado o
        // agente é anterior ao OTA — conta como desatualizado, que é a leitura correta para a TI.
        var mobilesAtivos = _db.Set<Mobile>().Where(m => m.UltimoHeartbeatEm != null);
        var naVersao = vigente is null
            ? 0
            : await mobilesAtivos.CountAsync(m => m.AgenteVersaoCode >= vigente.VersaoCode);
        var desatualizados = vigente is null
            ? 0
            : await mobilesAtivos.CountAsync(m => m.AgenteVersaoCode == null || m.AgenteVersaoCode < vigente.VersaoCode);

        return Ok(new SituacaoAgenteAndroidDto(vigente, historico, naVersao, desatualizados));
    }

    /// <summary>
    /// Publica um APK novo para a frota. Ação sensível (instala software em todos os aparelhos):
    /// exige a senha do ADM logado, como excluir dispositivo.
    /// </summary>
    // APKs do agente têm poucos MB; 100 MB dá folga sem liberar upload irrestrito.
    [HttpPost("pacotes")]
    [RequestSizeLimit(100_000_000)]
    public async Task<ActionResult<PacoteAgenteAndroidDto>> Publicar(
        [FromForm] PublicarPacoteAgenteForm form)
    {
        var adm = await _confirmacaoSenha.ConferirAsync(User, form.Senha);
        // 400, e não 401: o 401 faria o site tratar como sessão expirada e deslogar o ADM.
        if (adm is null) return BadRequest(new { message = "Senha incorreta." });

        if (form.Arquivo is null || form.Arquivo.Length == 0)
            return BadRequest(new { message = "Envie o arquivo .apk do agente." });

        if (!Path.GetExtension(form.Arquivo.FileName).Equals(".apk", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "O arquivo precisa ser um .apk." });

        if (form.VersaoCode <= 0 || string.IsNullOrWhiteSpace(form.VersaoNome))
            return BadRequest(new { message = "Informe o versionCode (inteiro positivo) e o versionName do APK." });

        // O Android recusa downgrade: publicar um versionCode menor ou igual ao vigente deixaria a
        // frota tentando (e falhando) para sempre. Barra aqui com mensagem clara.
        var maiorPublicado = await _db.PacotesAgenteAndroid
            .OrderByDescending(p => p.VersaoCode)
            .Select(p => (int?)p.VersaoCode)
            .FirstOrDefaultAsync();
        if (maiorPublicado is { } maior && form.VersaoCode <= maior)
            return BadRequest(new { message = $"O versionCode precisa ser maior que o vigente ({maior})." });

        await using var conteudo = form.Arquivo.OpenReadStream();

        // APK é um ZIP: os dois primeiros bytes são sempre "PK". Barra upload de arquivo errado
        // (um .apk renomeado) antes de gravar qualquer coisa.
        var assinatura = new byte[2];
        var lidos = await conteudo.ReadAtLeastAsync(assinatura, 2, throwOnEndOfStream: false);
        if (lidos < 2 || assinatura[0] != 'P' || assinatura[1] != 'K')
            return BadRequest(new { message = "O arquivo não parece ser um APK válido." });
        conteudo.Position = 0;

        var (caminho, hash, tamanho) = await _pacotes.SalvarPacoteAsync(conteudo);

        var pacote = new PacoteAgenteAndroid
        {
            VersaoCode = form.VersaoCode,
            VersaoNome = form.VersaoNome.Trim(),
            NomeArquivoOriginal = form.Arquivo.FileName,
            CaminhoRelativo = caminho,
            HashSha256 = hash,
            TamanhoBytes = tamanho,
            EnviadoPorAdmId = adm.Id,
        };
        _db.PacotesAgenteAndroid.Add(pacote);
        _auditoria.Registrar(AcaoAuditoria.ApkPublicado,
            $"Publicou o APK do agente v{pacote.VersaoNome} (código {pacote.VersaoCode}).",
            "PacoteAgenteAndroid", pacote.Id);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch
        {
            // O arquivo já estava em disco; sem o registro no banco ele viraria lixo órfão.
            _pacotes.Excluir(caminho);
            throw;
        }

        return Ok(new PacoteAgenteAndroidDto(
            pacote.Id, pacote.VersaoCode, pacote.VersaoNome, pacote.NomeArquivoOriginal,
            pacote.HashSha256, pacote.TamanhoBytes, pacote.CriadoEm));
    }
}

/// <summary>Formulário multipart do upload do APK. Classe (não record) para o binder de form.</summary>
public class PublicarPacoteAgenteForm
{
    public IFormFile? Arquivo { get; set; }
    public int VersaoCode { get; set; }
    public string VersaoNome { get; set; } = string.Empty;
    /// <summary>Senha do ADM logado — reconferida porque publicar um APK afeta a frota inteira.</summary>
    public string Senha { get; set; } = string.Empty;
}
