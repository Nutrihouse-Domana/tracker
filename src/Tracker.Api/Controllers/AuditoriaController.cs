using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tracker.Application.Common;
using Tracker.Application.Dtos;
using Tracker.Application.Seguranca;
using Tracker.Domain.Enums;
using Tracker.Infrastructure.Data;

namespace Tracker.Api.Controllers;

/// <summary>Consulta da trilha de auditoria (quem fez o quê e quando). Somente leitura.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = Permissoes.AuditoriaVer)]
public class AuditoriaController : ControllerBase
{
    private const int TamanhoMaximoPagina = 100;
    private readonly TrackerDbContext _db;

    public AuditoriaController(TrackerDbContext db) => _db = db;

    /// <summary>Rótulos em pt-BR dos tipos de ação, para o filtro da tela.</summary>
    [HttpGet("acoes")]
    public ActionResult<IEnumerable<AcaoAuditoriaDto>> ListarAcoes() =>
        Ok(Enum.GetValues<AcaoAuditoria>().Select(a => new AcaoAuditoriaDto(a, RotuloAcao(a))));

    [HttpGet]
    public async Task<ActionResult<PaginaAuditoriaDto>> Listar(
        [FromQuery] Guid? admId,
        [FromQuery] AcaoAuditoria? acao,
        [FromQuery] DateOnly? de,
        [FromQuery] DateOnly? ate,
        [FromQuery] string? entidadeTipo,
        [FromQuery] Guid? entidadeId,
        [FromQuery] string? q,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanho = 50)
    {
        pagina = Math.Max(1, pagina);
        tamanho = Math.Clamp(tamanho, 1, TamanhoMaximoPagina);

        var query = _db.LogsAuditoria.AsNoTracking().AsQueryable();

        if (admId is not null) query = query.Where(l => l.AdmId == admId);
        if (acao is not null) query = query.Where(l => l.Acao == acao);
        // De/até são DIAS CIVIS de Brasília (vêm do <input type="date">), e CriadoEm é UTC. Comparar
        // direto cortava os registros das 21h às 24h de Brasília (já "amanhã" em UTC) e trazia os
        // da véspera. Os limites viram instantes UTC; "até" é inclusivo no dia: início do dia
        // seguinte com "menor que".
        if (de is { } diaInicial)
        {
            var inicioUtc = FusoBrasil.InicioDoDiaEmUtc(diaInicial);
            query = query.Where(l => l.CriadoEm >= inicioUtc);
        }
        if (ate is { } diaFinal)
        {
            var fimUtc = FusoBrasil.FimDoDiaEmUtc(diaFinal);
            query = query.Where(l => l.CriadoEm < fimUtc);
        }
        if (!string.IsNullOrWhiteSpace(entidadeTipo)) query = query.Where(l => l.EntidadeTipo == entidadeTipo);
        if (entidadeId is not null) query = query.Where(l => l.EntidadeId == entidadeId);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var termo = q.Trim();
            query = query.Where(l => l.Descricao.Contains(termo) || l.AdmNome.Contains(termo));
        }

        var total = await query.CountAsync();

        var itens = await query
            .OrderByDescending(l => l.CriadoEm)
            .Skip((pagina - 1) * tamanho)
            .Take(tamanho)
            .Select(l => new LogAuditoriaDto(
                l.Id, l.CriadoEm, l.AdmId, l.AdmNome, l.Acao,
                l.EntidadeTipo, l.EntidadeId, l.Descricao, l.Detalhes, l.EnderecoIp))
            .ToListAsync();

        return Ok(new PaginaAuditoriaDto(itens, total, pagina, tamanho));
    }

    private static string RotuloAcao(AcaoAuditoria acao) => acao switch
    {
        AcaoAuditoria.DispositivoCriado => "Dispositivo cadastrado",
        AcaoAuditoria.DispositivoStatusAlterado => "Status do dispositivo alterado",
        AcaoAuditoria.DispositivoAtribuido => "Dispositivo atribuído",
        AcaoAuditoria.DispositivoUnidadeDefinida => "Unidade do dispositivo definida",
        AcaoAuditoria.DispositivoIdentificacaoInternaDefinida => "Identificação interna do dispositivo alterada",
        AcaoAuditoria.DispositivoPosseAlterada => "Posse do dispositivo alterada",
        AcaoAuditoria.DispositivoArquivado => "Dispositivo arquivado",
        AcaoAuditoria.DispositivoExcluidoDefinitivo => "Dispositivo excluído definitivo",
        AcaoAuditoria.DispositivoRestaurado => "Dispositivo restaurado",
        AcaoAuditoria.DispositivoVencimentoAluguelDefinido => "Vencimento do aluguel definido",
        AcaoAuditoria.DispositivoAluguelDefinido => "Aluguel da impressora alterado",
        AcaoAuditoria.ImpressoraConexaoDefinida => "Conexão da impressora alterada",
        AcaoAuditoria.ImpressoraDadosDefinidos => "Dados da impressora alterados",
        AcaoAuditoria.ReparoAberto => "Reparo aberto",
        AcaoAuditoria.ReparoStatusAlterado => "Status do reparo alterado",
        AcaoAuditoria.ComentarioAdicionado => "Comentário adicionado",
        AcaoAuditoria.ComandoBloquear => "Bloqueio remoto",
        AcaoAuditoria.ComandoDesbloquear => "Desbloqueio remoto",
        AcaoAuditoria.EnrollmentGerado => "Token do agente gerado",
        AcaoAuditoria.AlertaReconhecido => "Alerta reconhecido",
        AcaoAuditoria.ApkPublicado => "APK do agente publicado",
        AcaoAuditoria.TermoRegistrado => "Termo de uso registrado",
        AcaoAuditoria.DetentorCriado => "Detentor cadastrado",
        AcaoAuditoria.DetentorEditado => "Detentor editado",
        AcaoAuditoria.DetentorDesativado => "Detentor desativado",
        AcaoAuditoria.UnidadeCriada => "Unidade cadastrada",
        AcaoAuditoria.UnidadeEditada => "Unidade editada",
        AcaoAuditoria.AcessoCriado => "Login criado",
        AcaoAuditoria.AcessoEditado => "Login editado",
        AcaoAuditoria.AcessoAtivado => "Login ativado",
        AcaoAuditoria.AcessoDesativado => "Login desativado",
        AcaoAuditoria.AcessoPermissoesAlteradas => "Permissões alteradas",
        AcaoAuditoria.AcessoSenhaRedefinida => "Senha redefinida",
        AcaoAuditoria.LoginEfetuado => "Login efetuado",
        AcaoAuditoria.LoginRecusado => "Login recusado",
        _ => acao.ToString(),
    };
}
