using Microsoft.EntityFrameworkCore;
using Tracker.Domain.Entities;
using Tracker.Infrastructure.Data;
using Tracker.Infrastructure.Security;

namespace Tracker.Api.Agente;

/// <summary>
/// Resolve o dispositivo dono de um token de agente (header X-Agent-Token).
/// Como só notebooks e mobiles têm agente, filtra por DispositivoComAgente.
/// </summary>
public class AgentAuth
{
    public const string HeaderToken = "X-Agent-Token";

    private readonly TrackerDbContext _db;
    private readonly AgentTokenService _tokenService;

    public AgentAuth(TrackerDbContext db, AgentTokenService tokenService)
    {
        _db = db;
        _tokenService = tokenService;
    }

    /// <summary>Retorna o dispositivo autenticado pelo token, ou null se inválido.</summary>
    public async Task<DispositivoComAgente?> ResolverAsync(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;

        var hash = _tokenService.CalcularHash(token);

        // Consulta os tipos concretos (Notebook/Mobile) — é onde TokenAgenteHash existe e é
        // traduzível para SQL. O DbSet base não expõe a propriedade do tipo derivado.
        DispositivoComAgente? dispositivo = await _db.Set<Notebook>().FirstOrDefaultAsync(d => d.TokenAgenteHash == hash);
        dispositivo ??= await _db.Set<Mobile>().FirstOrDefaultAsync(d => d.TokenAgenteHash == hash);

        return dispositivo;
    }
}
