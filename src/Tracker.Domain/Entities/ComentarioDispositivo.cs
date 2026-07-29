using Tracker.Domain.Common;

namespace Tracker.Domain.Entities;

/// <summary>Comentário livre de um Usuario ADM sobre um dispositivo (observações do dia a dia de TI).</summary>
public class ComentarioDispositivo : EntidadeBase
{
    public Guid DispositivoId { get; set; }
    public Dispositivo? Dispositivo { get; set; }

    public string Texto { get; set; } = string.Empty;
    public Guid CriadoPorAdmId { get; set; }
}
