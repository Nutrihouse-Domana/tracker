using Tracker.Domain.Common;
using Tracker.Domain.Enums;

namespace Tracker.Domain.Entities;

/// <summary>Registro de reparo/manutenção de um dispositivo.</summary>
public class HistoricoReparo : EntidadeBase
{
    public Guid DispositivoId { get; set; }
    public Dispositivo? Dispositivo { get; set; }

    public string Descricao { get; set; } = string.Empty;
    public StatusReparo Status { get; set; } = StatusReparo.Aberto;
    public decimal? CustoEstimado { get; set; }
    public DateTime DataAbertura { get; set; } = DateTime.UtcNow;
    public DateTime? DataConclusao { get; set; }

    public Guid RegistradoPorAdmId { get; set; }
}
