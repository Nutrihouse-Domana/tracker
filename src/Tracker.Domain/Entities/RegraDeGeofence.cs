using Tracker.Domain.Common;
using Tracker.Domain.Enums;

namespace Tracker.Domain.Entities;

/// <summary>
/// Regra opt-in criada pela TI para restringir um dispositivo a uma área geográfica.
/// Padrão do sistema é bloquear automaticamente ao sair do raio; a TI pode optar por
/// apenas alertar (recomendado para notebooks, cuja localização por IP é menos precisa).
/// </summary>
public class RegraDeGeofence : EntidadeBase
{
    public Guid DispositivoId { get; set; }
    public Dispositivo? Dispositivo { get; set; }

    public Guid LocalId { get; set; }
    public Local? Local { get; set; }

    public double RaioMetros { get; set; }
    public bool Ativo { get; set; } = true;
    public AcaoAoSairDoGeofence AcaoAoSair { get; set; } = AcaoAoSairDoGeofence.BloquearAutomaticamente;

    public Guid CriadoPorAdmId { get; set; }
}
