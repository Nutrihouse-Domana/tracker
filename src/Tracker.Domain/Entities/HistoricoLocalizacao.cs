using Tracker.Domain.Common;
using Tracker.Domain.Enums;

namespace Tracker.Domain.Entities;

/// <summary>Ponto de localização reportado pelo agente (Notebook/Telefone) ou lançado manualmente pela TI (Periférico).</summary>
public class HistoricoLocalizacao : EntidadeBase
{
    public Guid DispositivoId { get; set; }
    public Dispositivo? Dispositivo { get; set; }

    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string? EnderecoAproximado { get; set; }
    public OrigemLocalizacao Origem { get; set; }
    public double? PrecisaoMetros { get; set; }
    public DateTime CapturadoEm { get; set; } = DateTime.UtcNow;
}
