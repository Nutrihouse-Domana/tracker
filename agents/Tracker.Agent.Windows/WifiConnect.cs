using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;

namespace Tracker.Agent.Windows;

/// <summary>
/// Wi-Fi dentro da tela de bloqueio: lista as redes ao alcance e conecta via netsh, para a máquina
/// voltar online (e receber o comando de Desbloquear da TI) sem sair da trava.
///
/// PEGADINHA (já corrigida): a listagem usa a <b>Native Wifi API</b> (wlanapi.dll), NÃO o
/// `netsh wlan show networks`. O netsh apenas devolve o cache da última varredura e NUNCA dispara
/// uma nova — numa máquina ociosa/desconectada (o caso da tela de bloqueio) ele retorna só a rede
/// conectada, ou nada. Além disso, versões de Windows sem `netsh wlan scan` não têm como forçar a
/// busca por linha de comando. A API nativa chama <c>WlanScan</c> (varredura de verdade), espera a
/// notificação de conclusão e enumera com <c>WlanGetAvailableNetworkList</c>. O netsh fica só como
/// fallback (e para conectar, que funciona bem uma vez que se tem o SSID).
/// </summary>
public static partial class WifiConnect
{
    public static bool TemInterfaceWifi()
    {
        try
        {
            if (NativeWifi.TentarObterInterfaces(out var guids) && guids.Count > 0) return true;
        }
        catch { /* cai no netsh */ }

        var saida = RodarNetsh("wlan show interfaces");
        return !saida.Contains("no wireless interface", StringComparison.OrdinalIgnoreCase)
            && !saida.Contains("sem fio no sistema", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Redes ao alcance, ordenadas por sinal (melhor primeiro). Dispara um scan real via API nativa;
    /// só cai para o netsh (cache) se a API falhar ou não devolver nada.
    /// </summary>
    public static List<string> ListarRedes()
    {
        try
        {
            var nativas = NativeWifi.ListarRedes();
            if (nativas.Count > 0) return nativas;
        }
        catch { /* cai no netsh */ }

        return ListarRedesNetsh();
    }

    private static List<string> ListarRedesNetsh()
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

/// <summary>
/// Acesso à Native Wifi API (wlanapi.dll). Só o necessário para forçar um scan e enumerar as redes
/// visíveis — o que o netsh não faz. Tudo isolado aqui para o resto do agente não lidar com P/Invoke.
/// </summary>
internal static class NativeWifi
{
    private const uint CLIENT_VERSION = 2;
    private const uint ERROR_SUCCESS = 0;

    // Fonte e códigos de notificação do Auto Configuration Module (ACM).
    private const uint WLAN_NOTIFICATION_SOURCE_ACM = 0x00000008;
    private const uint wlan_notification_acm_scan_complete = 7;
    private const uint wlan_notification_acm_scan_fail = 8;

    /// <summary>
    /// Faz uma varredura real e devolve os SSIDs visíveis (distintos, sem ocultos), do maior para o
    /// menor sinal. Agrega todas as interfaces Wi-Fi da máquina.
    /// </summary>
    public static List<string> ListarRedes()
    {
        var handle = Abrir();
        try
        {
            if (!TentarObterInterfaces(handle, out var guids)) return [];

            var porNome = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
            foreach (var guid in guids)
            {
                DispararScanEEsperar(handle, guid);
                foreach (var (ssid, sinal) in EnumerarRedes(handle, guid))
                {
                    // Mantém o melhor sinal visto para cada SSID.
                    if (!porNome.TryGetValue(ssid, out var atual) || sinal > atual) porNome[ssid] = sinal;
                }
            }

            return porNome.OrderByDescending(kv => kv.Value).Select(kv => kv.Key).ToList();
        }
        finally { WlanCloseHandle(handle, IntPtr.Zero); }
    }

    /// <summary>Versão pública usada por <see cref="WifiConnect.TemInterfaceWifi"/>: abre, lista e fecha.</summary>
    public static bool TentarObterInterfaces(out List<Guid> guids)
    {
        var handle = Abrir();
        try { return TentarObterInterfaces(handle, out guids); }
        finally { WlanCloseHandle(handle, IntPtr.Zero); }
    }

    private static IntPtr Abrir()
    {
        var r = WlanOpenHandle(CLIENT_VERSION, IntPtr.Zero, out _, out var handle);
        if (r != ERROR_SUCCESS) throw new InvalidOperationException($"WlanOpenHandle falhou ({r}).");
        return handle;
    }

    private static bool TentarObterInterfaces(IntPtr handle, out List<Guid> guids)
    {
        guids = [];
        if (WlanEnumInterfaces(handle, IntPtr.Zero, out var lista) != ERROR_SUCCESS) return false;
        try
        {
            int n = Marshal.ReadInt32(lista);                 // dwNumberOfItems
            int header = 8;                                    // dwNumberOfItems + dwIndex
            int stride = Marshal.SizeOf<WLAN_INTERFACE_INFO>();
            for (int i = 0; i < n; i++)
            {
                var info = Marshal.PtrToStructure<WLAN_INTERFACE_INFO>(lista + header + i * stride);
                guids.Add(info.InterfaceGuid);
            }
        }
        finally { WlanFreeMemory(lista); }
        return guids.Count > 0;
    }

    /// <summary>Dispara o scan e espera a notificação de conclusão (até ~5s), para a lista vir fresca.</summary>
    private static void DispararScanEEsperar(IntPtr handle, Guid guid)
    {
        using var concluido = new AutoResetEvent(false);
        var alvo = guid;

        // O callback precisa continuar vivo durante toda a espera (senão o GC o coleta).
        WlanNotificationCallback callback = (ref WLAN_NOTIFICATION_DATA data, IntPtr _) =>
        {
            if (data.NotificationSource == WLAN_NOTIFICATION_SOURCE_ACM &&
                (data.NotificationCode == wlan_notification_acm_scan_complete ||
                 data.NotificationCode == wlan_notification_acm_scan_fail) &&
                data.InterfaceGuid == alvo)
            {
                try { concluido.Set(); } catch { }
            }
        };

        bool registrado = WlanRegisterNotification(handle, WLAN_NOTIFICATION_SOURCE_ACM, false,
            callback, IntPtr.Zero, IntPtr.Zero, out _) == ERROR_SUCCESS;
        try
        {
            if (WlanScan(handle, ref alvo, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero) == ERROR_SUCCESS && registrado)
                concluido.WaitOne(TimeSpan.FromSeconds(5));
            else if (!registrado)
                Thread.Sleep(3000); // sem notificação, dá um tempo para a varredura popular o cache
        }
        finally
        {
            if (registrado)
                WlanRegisterNotification(handle, 0 /* NONE */, false, null, IntPtr.Zero, IntPtr.Zero, out _);
            GC.KeepAlive(callback);
        }
    }

    private static IEnumerable<(string ssid, uint sinal)> EnumerarRedes(IntPtr handle, Guid guid)
    {
        var resultado = new List<(string, uint)>();
        var alvo = guid;
        if (WlanGetAvailableNetworkList(handle, ref alvo, 0, IntPtr.Zero, out var lista) != ERROR_SUCCESS)
            return resultado;
        try
        {
            int n = Marshal.ReadInt32(lista);                       // dwNumberOfItems
            int header = 8;                                          // dwNumberOfItems + dwIndex
            int stride = Marshal.SizeOf<WLAN_AVAILABLE_NETWORK>();
            for (int i = 0; i < n; i++)
            {
                var rede = Marshal.PtrToStructure<WLAN_AVAILABLE_NETWORK>(lista + header + i * stride);
                int len = (int)Math.Min(rede.dot11Ssid.uSSIDLength, 32u);
                if (len <= 0) continue;                              // SSID oculto: sem nome para exibir
                var nome = Encoding.UTF8.GetString(rede.dot11Ssid.ucSSID, 0, len).Trim();
                if (nome.Length > 0) resultado.Add((nome, rede.wlanSignalQuality));
            }
        }
        finally { WlanFreeMemory(lista); }
        return resultado;
    }

    // ---- P/Invoke: estruturas ----

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WLAN_INTERFACE_INFO
    {
        public Guid InterfaceGuid;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string strInterfaceDescription;
        public uint isState;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DOT11_SSID
    {
        public uint uSSIDLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] ucSSID;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WLAN_AVAILABLE_NETWORK
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string strProfileName;
        public DOT11_SSID dot11Ssid;
        public uint dot11BssType;
        public uint uNumberOfBssids;
        [MarshalAs(UnmanagedType.Bool)] public bool bNetworkConnectable;
        public uint wlanNotConnectableReason;
        public uint uNumberOfPhyTypes;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)] public uint[] dot11PhyTypes;
        [MarshalAs(UnmanagedType.Bool)] public bool bMorePhyTypes;
        public uint wlanSignalQuality;
        [MarshalAs(UnmanagedType.Bool)] public bool bSecurityEnabled;
        public uint dot11DefaultAuthAlgorithm;
        public uint dot11DefaultCipherAlgorithm;
        public uint dwFlags;
        public uint dwReserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WLAN_NOTIFICATION_DATA
    {
        public uint NotificationSource;
        public uint NotificationCode;
        public Guid InterfaceGuid;
        public uint dwDataSize;
        public IntPtr pData;
    }

    private delegate void WlanNotificationCallback(ref WLAN_NOTIFICATION_DATA data, IntPtr context);

    // ---- P/Invoke: funções ----

    [DllImport("wlanapi.dll")]
    private static extern uint WlanOpenHandle(uint dwClientVersion, IntPtr pReserved, out uint pdwNegotiatedVersion, out IntPtr phClientHandle);

    [DllImport("wlanapi.dll")]
    private static extern uint WlanCloseHandle(IntPtr hClientHandle, IntPtr pReserved);

    [DllImport("wlanapi.dll")]
    private static extern uint WlanEnumInterfaces(IntPtr hClientHandle, IntPtr pReserved, out IntPtr ppInterfaceList);

    [DllImport("wlanapi.dll")]
    private static extern uint WlanScan(IntPtr hClientHandle, ref Guid pInterfaceGuid, IntPtr pDot11Ssid, IntPtr pIeData, IntPtr pReserved);

    [DllImport("wlanapi.dll")]
    private static extern uint WlanGetAvailableNetworkList(IntPtr hClientHandle, ref Guid pInterfaceGuid, uint dwFlags, IntPtr pReserved, out IntPtr ppAvailableNetworkList);

    [DllImport("wlanapi.dll")]
    private static extern uint WlanRegisterNotification(IntPtr hClientHandle, uint dwNotifSource, [MarshalAs(UnmanagedType.Bool)] bool bIgnoreDuplicate, WlanNotificationCallback? funcCallback, IntPtr pCallbackContext, IntPtr pReserved, out uint pdwPrevNotifSource);

    [DllImport("wlanapi.dll")]
    private static extern void WlanFreeMemory(IntPtr pMemory);
}
