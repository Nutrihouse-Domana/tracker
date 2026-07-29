using System.Diagnostics;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;

namespace Tracker.Agent.Windows;

/// <summary>
/// Conexão a Wi-Fi via netsh, usada dentro da tela de bloqueio para permitir que a máquina
/// volte a ficar online (e assim receber o comando de Desbloquear da TI) sem sair da trava.
/// </summary>
public static partial class WifiConnect
{
    public static bool TemInterfaceWifi()
    {
        var saida = RodarNetsh("wlan show interfaces");
        return !saida.Contains("no wireless interface", StringComparison.OrdinalIgnoreCase)
            && !saida.Contains("sem fio no sistema", StringComparison.OrdinalIgnoreCase);
    }

    public static List<string> ListarRedes()
    {
        var saida = RodarNetsh("wlan show networks");
        var redes = new List<string>();
        foreach (Match m in SsidRegex().Matches(saida))
        {
            var nome = m.Groups[1].Value.Trim();
            if (nome.Length > 0 && !redes.Contains(nome)) redes.Add(nome);
        }
        return redes;
    }

    public static (bool ok, string mensagem) Conectar(string ssid, string senha)
    {
        if (string.IsNullOrWhiteSpace(ssid)) return (false, "Selecione uma rede.");

        var arquivo = Path.Combine(Path.GetTempPath(), $"wifi-{Guid.NewGuid():N}.xml");
        try
        {
            File.WriteAllText(arquivo, MontarPerfil(ssid, senha), new UTF8Encoding(false));
            RodarNetsh($"wlan add profile filename=\"{arquivo}\" user=current");
            var r = RodarNetsh($"wlan connect name=\"{ssid}\" ssid=\"{ssid}\"");

            // netsh responde algo como "A solicitação de conexão foi concluída com êxito."
            var ok = r.Contains("êxito", StringComparison.OrdinalIgnoreCase)
                  || r.Contains("success", StringComparison.OrdinalIgnoreCase)
                  || r.Contains("concluíd", StringComparison.OrdinalIgnoreCase);
            return (ok, ok ? "Conectando… aguarde alguns segundos." : $"Falha ao conectar. {r.Trim()}");
        }
        catch (Exception ex)
        {
            return (false, "Erro ao conectar: " + ex.Message);
        }
        finally
        {
            try { if (File.Exists(arquivo)) File.Delete(arquivo); } catch { }
        }
    }

    private static string MontarPerfil(string ssid, string senha)
    {
        var s = SecurityElement.Escape(ssid);
        if (string.IsNullOrEmpty(senha))
        {
            // Rede aberta
            return $"""
            <?xml version="1.0"?>
            <WLANProfile xmlns="http://www.microsoft.com/networking/WLAN/profile/v1">
              <name>{s}</name>
              <SSIDConfig><SSID><name>{s}</name></SSID></SSIDConfig>
              <connectionType>ESS</connectionType>
              <connectionMode>auto</connectionMode>
              <MSM><security>
                <authEncryption><authentication>open</authentication><encryption>none</encryption><useOneX>false</useOneX></authEncryption>
              </security></MSM>
            </WLANProfile>
            """;
        }

        var p = SecurityElement.Escape(senha);
        return $"""
        <?xml version="1.0"?>
        <WLANProfile xmlns="http://www.microsoft.com/networking/WLAN/profile/v1">
          <name>{s}</name>
          <SSIDConfig><SSID><name>{s}</name></SSID></SSIDConfig>
          <connectionType>ESS</connectionType>
          <connectionMode>auto</connectionMode>
          <MSM><security>
            <authEncryption><authentication>WPA2PSK</authentication><encryption>AES</encryption><useOneX>false</useOneX></authEncryption>
            <sharedKey><keyType>passPhrase</keyType><protected>false</protected><keyMaterial>{p}</keyMaterial></sharedKey>
          </security></MSM>
        </WLANProfile>
        """;
    }

    private static string RodarNetsh(string args)
    {
        try
        {
            var psi = new ProcessStartInfo("netsh.exe", args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
            };
            using var p = Process.Start(psi)!;
            var saida = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
            p.WaitForExit(15000);
            return saida;
        }
        catch (Exception ex) { return ex.Message; }
    }

    [GeneratedRegex(@"(?im)^\s*SSID\s+\d+\s*:\s*(.+?)\s*$")]
    private static partial Regex SsidRegex();
}
