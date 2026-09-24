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
        ],
        StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// True quando o serial não identifica o aparelho: nulo, vazio, só espaços, só zeros ou um
    /// dos valores genéricos conhecidos de BIOS.
    /// </summary>
    public static bool EhGenerico(string? numeroSerie)
    {
        if (string.IsNullOrWhiteSpace(numeroSerie)) return true;

        var valor = numeroSerie.Trim();
        if (valor.All(c => c == '0')) return true;

        return ValoresConhecidos.Contains(valor);
    }
}
