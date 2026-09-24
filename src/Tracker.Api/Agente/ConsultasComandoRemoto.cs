using Microsoft.EntityFrameworkCore;
using Tracker.Domain.Entities;
using Tracker.Domain.Enums;
using Tracker.Infrastructure.Data;

namespace Tracker.Api.Agente;

/// <summary>Consultas de comando remoto usadas por mais de um controller.</summary>
public static class ConsultasComandoRemoto
{
    /// <summary>
    /// O comando de bloqueio/desbloqueio que vale agora: o mais recente emitido que não falhou
    /// (pendente, enviado ou confirmado). Nulo quando o aparelho nunca recebeu comando. Alimenta
    /// <see cref="Tracker.Application.Dispositivos.RegrasDispositivo.DeveEstarBloqueado"/> (heartbeat)
    /// e a trava da exclusão.
    /// </summary>
    public static Task<ComandoRemoto?> UltimoComandoVigenteAsync(this TrackerDbContext db, Guid dispositivoId) =>
        db.ComandosRemotos.AsNoTracking()
            .Where(c => c.DispositivoId == dispositivoId && c.Status != StatusComandoRemoto.Falhou)
            .OrderByDescending(c => c.DataEmissao)
            .ThenByDescending(c => c.CriadoEm)
            .FirstOrDefaultAsync();
}
