namespace Tracker.Application.Common;

/// <summary>
/// Números de série "de mentira" que a BIOS/placa-mãe devolve quando o fabricante não gravou o
/// serial (comum em desktops montados e em algumas placas OEM). Vários aparelhos diferentes
/// reportam o MESMO valor — usá-lo como identidade faz uma máquina "reencontrar" o cadastro da
/// outra no auto-enrollment e roubar o token dela.
/// </summary>
/// <remarks>
/// O agente Windows mantém uma lista equivalente (<c>agents/Tracker.Agent.Windows</c>): ao mudar
/// aqui, mude lá também.
/// </remarks>
public static class SerialGenerico
{
    /// <summary>Valores conhecidos, comparados sem diferenciar maiúsculas e após <c>Trim</c>.</summary>
    public static readonly IReadOnlySet<string> ValoresConhecidos = new HashSet<string>(
        [
            "To be filled by O.E.M.",
            "To Be Filled By O.E.M.",
            "Default string",
            "System Serial Number",
            "0",
            "None",
            "N/A",
            "Not Specified",
            "Not Applicable",
            "123456789",
            "0123456789",
            "Chassis Serial Number",
            "Base Board Serial Number",
            "INVALID",
            "XXXXXXXXXX",
            "To Be Filled By O.E.M",
            "NA",
            "Serial Number",
            "SerialNumber",
            "OEM",
            "Not Available",
            "Unknown",
        ],
        StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// True quando o serial não identifica o aparelho: nulo, vazio, só espaços, um único caractere
    /// repetido ("00000000", "XXXXXXXX"), qualquer texto com "O.E.M" ou um dos valores genéricos
    /// conhecidos de BIOS. Mesma regra de <c>InfoMaquina.EhSerialGenerico</c> no agente Windows.
    /// </summary>
    public static bool EhGenerico(string? numeroSerie)
    {
        if (string.IsNullOrWhiteSpace(numeroSerie)) return true;

        var valor = numeroSerie.Trim();
        if (ValoresConhecidos.Contains(valor)) return true;
        if (valor.Contains("O.E.M", StringComparison.OrdinalIgnoreCase)) return true;

        var primeiro = char.ToUpperInvariant(valor[0]);
        return valor.All(c => char.ToUpperInvariant(c) == primeiro);
    }
}
