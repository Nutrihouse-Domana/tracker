using Tracker.Domain.Common;

namespace Tracker.Domain.Entities;

/// <summary>
/// Uma consulta SNMP a uma impressora. Guarda o histórico do contador de páginas (conferência da
/// franquia do contrato de locação) e dos suprimentos. <c>CriadoEm</c> é o momento da leitura.
/// </summary>
public class LeituraImpressora : EntidadeBase
{
    public Guid ImpressoraId { get; set; }
    public Impressora? Impressora { get; set; }

    public bool Online { get; set; }
    public long? ContadorPaginas { get; set; }
    public string? EstadoReportado { get; set; }
    /// <summary>JSON de <c>[{descricao, cor, percentual}]</c>, igual a <see cref="Impressora.SuprimentosJson"/>.</summary>
    public string? SuprimentosJson { get; set; }
    public string? Erro { get; set; }
}
