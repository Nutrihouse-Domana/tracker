using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Hosting.WindowsServices;

namespace Tracker.Agent.Windows;

/// <summary>
/// Controla a trava do dispositivo. O bloqueio é uma tela cheia (LockOverlay) que roda na sessão
/// do usuário. A trava é PERSISTENTE E LOCAL: enquanto o arquivo-sinal (Caminhos.FlagBloqueio)
/// existir, o dispositivo deve permanecer bloqueado — mesmo offline e após reiniciar. Só a TI
/// remove o sinal, via "Desbloquear". O agente re-trava sozinho (GarantirBloqueioAtivo).
///
/// COMO O SERVIÇO SABE QUE A TELA ESTÁ VIVA: pelo PROCESSO <c>--overlay</c> em cada sessão
/// interativa ativa (WTSEnumerateSessions + Win32_Process.SessionId), não por arquivo. O marcador
/// antigo (overlay-alive.flag em %ProgramData%) era criado pelo usuário A e ficava sem permissão de
/// escrita para o usuário B: a tela de B nunca "provava" estar viva e o serviço disparava a tarefa
/// a cada 2s, empilhando processos. O processo não depende de permissão de pasta, e a pasta de
/// dados passou a ser somente leitura para usuários.
/// </summary>
public static class BloqueioTela
{
    /// <summary>Estado do relançamento por sessão, para não disparar a tarefa a cada 2s quando a
    /// tela não sobe (tarefa quebrada, sessão travada): recuo 10s → 20s → 40s → 80s → 120s.</summary>
    private static readonly Dictionary<int, (DateTime Ultimo, int Tentativas)> Relancamentos = new();
    private static readonly object Trava = new();

    /// <summary>Ativa o bloqueio: grava o sinal (persistente) e lança a tela.</summary>
    public static bool Travar(ILogger logger)
    {
        try
        {
            Directory.CreateDirectory(Caminhos.PastaDados);
            File.WriteAllText(Caminhos.FlagBloqueio, DateTime.Now.ToString("o"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Não foi possível criar o sinal de bloqueio em {Caminho}.", Caminhos.FlagBloqueio);
            return false;
        }

        lock (Trava) Relancamentos.Clear(); // comando novo: sem recuo pendente
        return LancarSeNecessario(logger);
    }

    /// <summary>
    /// Remove o bloqueio (apaga o sinal). A tela aberta se fecha sozinha em até 500ms e desliga
    /// a política do Gerenciador de Tarefas. Como serviço, dispara também a tarefa da tela: sem o
    /// sinal ela só executa a LIMPEZA (remove DisableTaskMgr do HKCU) e sai — cobre a tela que
    /// crashou ou foi morta antes de conseguir limpar.
    /// </summary>
    public static void Destravar(ILogger logger)
    {
        try
        {
            if (File.Exists(Caminhos.FlagBloqueio)) File.Delete(Caminhos.FlagBloqueio);
            logger.LogInformation("Bloqueio removido (a tela de bloqueio se fecha em seguida).");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Não foi possível remover o sinal de bloqueio.");
            return;
        }

        lock (Trava) Relancamentos.Clear();
        if (WindowsServiceHelpers.IsWindowsService() && !DispararTarefa(logger))
            logger.LogWarning("Não foi possível disparar a limpeza pós-desbloqueio na sessão do usuário.");
    }

    /// <summary>
    /// Chamado periodicamente pelo agente: se o dispositivo deve estar bloqueado (sinal existe)
    /// mas alguma sessão ativa está sem tela, (re)lança a tela. É isto que mantém a trava após
    /// reiniciar e mesmo sem rede, e que faz a tela reaparecer se alguém conseguir fechá-la.
    /// </summary>
    public static void GarantirBloqueioAtivo(ILogger logger)
    {
        if (File.Exists(Caminhos.FlagBloqueio)) LancarSeNecessario(logger);
    }

    /// <summary>Lança a tela só se houver sessão ativa sem ela, e respeitando o recuo.</summary>
    private static bool LancarSeNecessario(ILogger logger)
    {
        var semTela = SessoesSemTela();
        var agora = DateTime.UtcNow;

        lock (Trava)
        {
            // Sessões que já têm tela (ou saíram) zeram o recuo.
            foreach (var id in Relancamentos.Keys.Where(k => !semTela.Contains(k)).ToList())
                Relancamentos.Remove(id);

            if (semTela.Count == 0) return true;

            var liberadas = semTela.Where(id =>
                !Relancamentos.TryGetValue(id, out var r) || agora - r.Ultimo >= Recuo(r.Tentativas)).ToList();
            if (liberadas.Count == 0) return true; // ainda no prazo de espera da última tentativa

            foreach (var id in liberadas)
            {
                var tentativas = Relancamentos.TryGetValue(id, out var r) ? r.Tentativas + 1 : 1;
                Relancamentos[id] = (agora, tentativas);
                if (tentativas > 1)
                    logger.LogWarning("Sessão {Sessao} continua sem tela de bloqueio ({N}ª tentativa).", id, tentativas);
            }
        }

        logger.LogInformation("Ativando/reativando a tela de bloqueio (sessões sem tela: {Sessoes}).",
            string.Join(", ", semTela));
        return LancarTela(logger);
    }

    /// <summary>Espera mínima antes de relançar numa sessão que já recebeu N disparos sem a tela
    /// aparecer. O primeiro prazo (10s) também cobre o tempo de a tela subir.</summary>
    private static TimeSpan Recuo(int tentativas) =>
        TimeSpan.FromSeconds(Math.Min(120, 10 * Math.Pow(2, Math.Max(0, tentativas - 1))));

    /// <summary>Sessões interativas ATIVAS que não têm um processo de tela de bloqueio.</summary>
    private static HashSet<int> SessoesSemTela()
    {
        var sessoes = SessoesAtivas();
        if (sessoes.Count == 0) return [];
        sessoes.ExceptWith(SessoesComTela());
        return sessoes;
    }

    /// <summary>
    /// Sessões de usuário em estado ativo (na tela agora). Como console (teste), só a sessão do
    /// próprio processo. A sessão 0 (serviços) nunca conta.
    /// </summary>
    private static HashSet<int> SessoesAtivas()
    {
        if (!WindowsServiceHelpers.IsWindowsService())
            return [Process.GetCurrentProcess().SessionId];

        var resultado = new HashSet<int>();
        if (!WTSEnumerateSessions(IntPtr.Zero, 0, 1, out var lista, out var quantidade)) return resultado;
        try
        {
            var tamanho = Marshal.SizeOf<WTS_SESSION_INFO>();
            for (var i = 0; i < quantidade; i++)
            {
                var info = Marshal.PtrToStructure<WTS_SESSION_INFO>(lista + i * tamanho);
                if (info.State == WTSActive && info.SessionId != 0) resultado.Add(info.SessionId);
            }
        }
        finally { WTSFreeMemory(lista); }
        return resultado;
    }

    /// <summary>
    /// Sessões onde há um processo do agente em modo <c>--overlay</c>. A linha de comando vem do
    /// WMI (o <see cref="Process"/> não a expõe); se o WMI falhar, cai para "qualquer processo do
    /// agente fora da sessão 0" — no pior caso deixa de relançar, nunca empilha.
    /// </summary>
    private static HashSet<int> SessoesComTela()
    {
        var meuPid = Environment.ProcessId;
        var nomeExe = Path.GetFileName(Environment.ProcessPath) ?? "Tracker.Agent.Windows.exe";
        var sessoes = new HashSet<int>();

        try
        {
            using var busca = new ManagementObjectSearcher(
                $"SELECT ProcessId, SessionId, CommandLine FROM Win32_Process WHERE Name = '{nomeExe}'");
            foreach (var p in busca.Get())
            {
                var pid = Convert.ToInt32(p["ProcessId"]);
                var sessao = Convert.ToInt32(p["SessionId"]);
                var linha = p["CommandLine"]?.ToString() ?? "";
                if (pid != meuPid && sessao != 0 && linha.Contains("--overlay", StringComparison.OrdinalIgnoreCase))
                    sessoes.Add(sessao);
            }
            return sessoes;
        }
        catch
        {
            foreach (var p in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(nomeExe)))
            {
                using (p)
                {
                    try { if (p.Id != meuPid && p.SessionId != 0) sessoes.Add(p.SessionId); } catch { }
                }
            }
            return sessoes;
        }
    }

    private static bool LancarTela(ILogger logger)
    {
        try
        {
            if (WindowsServiceHelpers.IsWindowsService())
            {
                // Serviço (sessão 0): dispara a tarefa que roda a tela na sessão interativa do
                // usuário. Instância repetida numa sessão que já tem tela sai sozinha (mutex).
                if (!DispararTarefa(logger)) return false;
            }
            else
            {
                var exe = Environment.ProcessPath!;
                Process.Start(new ProcessStartInfo(exe, "--overlay") { UseShellExecute = false })?.Dispose();
            }

            logger.LogInformation("Tela de bloqueio lançada.");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro ao lançar a tela de bloqueio.");
            return false;
        }
    }

    /// <summary>schtasks /Run da tarefa da tela. Com o sinal presente ela trava; sem ele, só limpa.</summary>
    private static bool DispararTarefa(ILogger logger)
    {
        try
        {
            var psi = new ProcessStartInfo("schtasks.exe", $"/Run /TN \"{Caminhos.NomeTarefaBloqueio}\"")
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
            };
            using var p = Process.Start(psi)!;
            var saida = p.StandardOutput.ReadToEndAsync();
            var erro = p.StandardError.ReadToEndAsync();
            if (!p.WaitForExit(10000))
            {
                logger.LogError("schtasks não respondeu em 10s ao disparar a tarefa de bloqueio.");
                try { p.Kill(); } catch { }
                return false;
            }
            if (p.ExitCode != 0)
            {
                logger.LogError("Falha ao disparar a tarefa de bloqueio (schtasks {Code}): {Saida}",
                    p.ExitCode, (saida.Result + erro.Result).Trim());
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro ao disparar a tarefa de bloqueio.");
            return false;
        }
    }

    // ---- P/Invoke: sessões do Terminal Services ----

    private const int WTSActive = 0;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WTS_SESSION_INFO
    {
        public int SessionId;
        public IntPtr pWinStationName;
        public int State;
    }

    [DllImport("wtsapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool WTSEnumerateSessions(IntPtr hServer, int reserved, int version,
        out IntPtr ppSessionInfo, out int pCount);

    [DllImport("wtsapi32.dll")]
    private static extern void WTSFreeMemory(IntPtr pMemory);
}
