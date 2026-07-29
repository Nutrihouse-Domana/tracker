using Tracker.Domain.Common;
using Tracker.Domain.Enums;

namespace Tracker.Domain.Entities;

/// <summary>Comando de bloqueio/desbloqueio emitido para um dispositivo, entregue via SignalR
/// e confirmado pelo agente. Somente Usuario ADM pode emitir Desbloquear.</summary>
public class ComandoRemoto : EntidadeBase
{
    public Guid DispositivoId { get; set; }
    public Dispositivo? Dispositivo { get; set; }

    public TipoComandoRemoto TipoComando { get; set; }
    public Guid EmitidoPorAdmId { get; set; }
    public DateTime DataEmissao { get; set; } = DateTime.UtcNow;
    public StatusComandoRemoto Status { get; set; } = StatusComandoRemoto.Pendente;
    public DateTime? DataConfirmacao { get; set; }
}
