using Tracker.Domain.Common;

namespace Tracker.Domain.Entities;

/// <summary>Ponto geográfico de referência: endereço de filial/escritório, usado como centro de geofence
/// ou como localização registrada manualmente para periféricos.</summary>
public class Local : EntidadeBase
{
    public string Nome { get; set; } = string.Empty;
    public string Endereco { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}
