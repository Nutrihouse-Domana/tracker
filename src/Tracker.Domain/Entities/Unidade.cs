using Tracker.Domain.Common;

namespace Tracker.Domain.Entities;

/// <summary>
/// Unidade organizacional da Nutrihouse (matriz, filial, CD) que o dispositivo atende.
/// É diferente de <see cref="Local"/>: Local é um ponto geográfico usado como centro de geofence;
/// Unidade responde "a serviço de quem esse ativo está" e não exige coordenadas.
/// </summary>
public class Unidade : EntidadeBase
{
    public string Nome { get; set; } = string.Empty;
    /// <summary>Sigla/código interno (ex.: MTZ, FIL01). Opcional.</summary>
    public string? Codigo { get; set; }
    public bool Ativa { get; set; } = true;

    public ICollection<Dispositivo> Dispositivos { get; set; } = new List<Dispositivo>();
}
