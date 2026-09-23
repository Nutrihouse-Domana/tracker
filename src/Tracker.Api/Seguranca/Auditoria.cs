using Tracker.Domain.Entities;
using Tracker.Domain.Enums;
using Tracker.Infrastructure.Data;

namespace Tracker.Api.Seguranca;

/// <summary>
/// Registra uma entrada na trilha de auditoria. A autoria (quem/IP) é resolvida sozinha a partir do
/// ADM logado — o chamador só descreve O QUÊ aconteceu.
/// </summary>
public interface IAuditoria
{
    /// <summary>
    /// Enfileira um registro de auditoria no DbContext. Ele é gravado no mesmo <c>SaveChangesAsync</c>
    /// que o chamador já executa — logo, deve ser chamado ANTES desse save (e, na exclusão
    /// definitiva, antes do <c>Remove</c> do alvo).
    /// </summary>
    void Registrar(
        AcaoAuditoria acao,
        string descricao,
        string? entidadeTipo = null,
        Guid? entidadeId = null,
        string? detalhes = null);
}

/// <summary>
/// Implementação sobre o <see cref="TrackerDbContext"/> corrente: o log entra na mesma transação da
/// ação auditada, então ou os dois persistem, ou nenhum. Autor e IP vêm do <see cref="IHttpContextAccessor"/>.
/// </summary>
public class AuditoriaService : IAuditoria
{
    private readonly TrackerDbContext _db;
    private readonly IHttpContextAccessor _http;

    public AuditoriaService(TrackerDbContext db, IHttpContextAccessor http)
    {
        _db = db;
        _http = http;
    }

    public void Registrar(
        AcaoAuditoria acao,
        string descricao,
        string? entidadeTipo = null,
        Guid? entidadeId = null,
        string? detalhes = null)
    {
        var principal = _http.HttpContext?.User;

        _db.LogsAuditoria.Add(new LogAuditoria
        {
            AdmId = principal?.AdmId(),
            AdmNome = principal?.AdmNome() ?? "Sistema",
            Acao = acao,
            EntidadeTipo = entidadeTipo,
            EntidadeId = entidadeId,
            Descricao = descricao,
            Detalhes = detalhes,
            EnderecoIp = _http.HttpContext?.Connection.RemoteIpAddress?.ToString(),
        });
    }
}
