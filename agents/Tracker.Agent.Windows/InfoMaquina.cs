using System.Management;

namespace Tracker.Agent.Windows;

/// <summary>Coleta identificadores da máquina para o auto-cadastro (número de série, SO).</summary>
public static class InfoMaquina
{
    public static string NumeroSerie()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BIOS");
            foreach (var obj in searcher.Get())
            {
                var serie = obj["SerialNumber"]?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(serie) && serie != "0" && !serie.Contains("None", StringComparison.OrdinalIgnoreCase))
                    return serie;
            }
        }
        catch { /* WMI indisponível — cai no fallback abaixo */ }

        // Fallback: nome da máquina (garante um identificador estável e não vazio).
        return $"HOST-{Environment.MachineName}";
    }

    public static string SistemaOperacional() => System.Runtime.InteropServices.RuntimeInformation.OSDescription;

    public static string? MacAddress()
    {
        try
        {
            return System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(n => n.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up
                    && n.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback)
                ?.GetPhysicalAddress().ToString();
        }
        catch { return null; }
    }
}
