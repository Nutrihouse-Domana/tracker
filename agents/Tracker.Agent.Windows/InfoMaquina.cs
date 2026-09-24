using System.Management;
using Microsoft.Win32;

namespace Tracker.Agent.Windows;

/// <summary>Coleta identificadores da máquina para o auto-cadastro (número de série, SO).</summary>
public static class InfoMaquina
{
    /// <summary>
    /// Seriais "de fábrica" que NÃO identificam nada: placas genéricas, montadoras que não gravam o
    /// campo e VMs saem todas com o mesmo texto. Usados como identidade, fariam várias máquinas
    /// caírem no MESMO registro do inventário (o auto-cadastro reencontra pelo número de série).
    /// Comparação sem diferenciar maiúsculas, após trim. O backend recusa estes valores com 400.
    /// </summary>
    private static readonly HashSet<string> SeriaisGenericos = new(StringComparer.OrdinalIgnoreCase)
    {
        "To be filled by O.E.M.", "To Be Filled By O.E.M", "Default string", "System Serial Number",
        "0", "None", "N/A", "NA", "Not Specified", "Not Applicable", "123456789", "0123456789",
        "Chassis Serial Number", "Base Board Serial Number", "INVALID", "Serial Number", "OEM",
        "SerialNumber", "Not Available", "Unknown",
    };

    /// <summary>UUIDs de SMBIOS que também vêm repetidos de fábrica em placas genéricas.</summary>
    private static readonly HashSet<string> UuidsGenericos = new(StringComparer.OrdinalIgnoreCase)
    {
        "03000200-0400-0500-0006-000700080009",
    };

    /// <summary>
    /// Número de série para o auto-cadastro. Ordem: serial da BIOS (se não for genérico) →
    /// <c>WIN-&lt;UUID do SMBIOS&gt;</c> → <c>WIN-&lt;MachineGuid&gt;</c> → <c>HOST-&lt;nome&gt;</c>.
    /// O prefixo <c>WIN-</c> marca identificador provisório estável por máquina (não é o serial
    /// da etiqueta); o <c>HOST-</c> é o último recurso e muda se a máquina for renomeada.
    /// </summary>
    public static string NumeroSerie()
    {
        var serieBios = LerWmi("SELECT SerialNumber FROM Win32_BIOS", "SerialNumber");
        if (!EhSerialGenerico(serieBios)) return serieBios!.Trim();

        var uuid = LerWmi("SELECT UUID FROM Win32_ComputerSystemProduct", "UUID");
        if (UuidValido(uuid)) return $"WIN-{NormalizarGuid(uuid!)}";

        var machineGuid = LerMachineGuid();
        if (UuidValido(machineGuid)) return $"WIN-{NormalizarGuid(machineGuid!)}";

        return $"HOST-{Environment.MachineName}";
    }

    /// <summary>Serial vazio, da lista de genéricos ou feito de um único caractere repetido
    /// ("00000000", "XXXXXXXX", "        ").</summary>
    public static bool EhSerialGenerico(string? serie)
    {
        if (string.IsNullOrWhiteSpace(serie)) return true;
        var s = serie.Trim();
        if (SeriaisGenericos.Contains(s)) return true;
        if (s.Contains("O.E.M", StringComparison.OrdinalIgnoreCase)) return true;
        return s.All(c => char.ToUpperInvariant(c) == char.ToUpperInvariant(s[0]));
    }

    /// <summary>GUID bem formado e que não seja todo 0/F nem um UUID genérico conhecido.</summary>
    private static bool UuidValido(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor) || !Guid.TryParse(valor.Trim(), out var guid)) return false;
        var hex = guid.ToString("N");
        if (hex.All(c => c == '0') || hex.All(c => c is 'f' or 'F')) return false;
        return !UuidsGenericos.Contains(guid.ToString("D"));
    }

    private static string NormalizarGuid(string valor) => Guid.Parse(valor.Trim()).ToString("D").ToUpperInvariant();

    private static string? LerWmi(string consulta, string propriedade)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(consulta);
            foreach (var obj in searcher.Get())
            {
                var valor = obj[propriedade]?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(valor)) return valor;
            }
        }
        catch { /* WMI indisponível — o chamador cai no próximo fallback */ }
        return null;
    }

    /// <summary>MachineGuid do Windows (gerado na instalação do SO). Lido na visão de 64 bits do
    /// registro: num processo 32 bits a visão padrão cairia no WOW6432Node, onde ele não existe.</summary>
    private static string? LerMachineGuid()
    {
        try
        {
            using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var chave = hklm.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            return chave?.GetValue("MachineGuid") as string;
        }
        catch { return null; }
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
