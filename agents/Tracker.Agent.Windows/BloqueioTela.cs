using System.Diagnostics;
using Microsoft.Extensions.Hosting.WindowsServices;

namespace Tracker.Agent.Windows;

/// <summary>
/// Controla a trava do dispositivo. O bloqueio é uma tela cheia (LockOverlay) que roda na sessão
/// do usuário. A trava é PERSISTENTE E LOCAL: enquanto o arquivo-sinal (Caminhos.FlagBloqueio)
/// existir, o dispositivo deve permanecer bloqueado — mesmo offline e após reiniciar. Só a TI
/// remove o sinal, via "Desbloquear". O agente re-trava sozinho (GarantirBloqueioAtivo).
/// </summary>
public static class BloqueioTela
{
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

        return LancarSeNecessario(logger);
    }

    /// <summary>Remove o bloqueio (apaga o sinal). A tela se fecha em seguida sozinha.</summary>
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
        }
    }

    /// <summary>
    /// Chamado periodicamente pelo agente: se o dispositivo deve estar bloqueado (sinal existe)
    /// mas nenhuma tela está aberta, (re)lança a tela. É isto que mantém a trava após reiniciar
    /// e mesmo sem rede, e que faz a tela reaparecer se alguém conseguir fechá-la.
    /// </summary>
    public static void GarantirBloqueioAtivo(ILogger logger)
    {
        if (File.Exists(Caminhos.FlagBloqueio)) LancarSeNecessario(logger);
    }

    /// <summary>Lança a tela apenas se ainda não houver uma aberta (dedup via marcador).</summary>
    private static bool LancarSeNecessario(ILogger logger)
    {
        if (TelaAberta()) return true;
        logger.LogInformation("Ativando/reativando a tela de bloqueio.");
        return LancarTela(logger);
    }

    /// <summary>A tela de bloqueio está na tela agora? (marcador atualizado nos últimos ~6s)</summary>
    private static bool TelaAberta()
    {
        try
        {
            if (!File.Exists(Caminhos.MarcadorOverlay)) return false;
            return (DateTime.Now - File.GetLastWriteTime(Caminhos.MarcadorOverlay)).TotalSeconds < 6;
        }
        catch { return false; }
    }

    private static bool LancarTela(ILogger logger)
    {
        try
        {
            if (WindowsServiceHelpers.IsWindowsService())
            {
                // Serviço (sessão 0): dispara a tarefa que roda a tela na sessão interativa do usuário.
                var psi = new ProcessStartInfo("schtasks.exe", $"/Run /TN \"{Caminhos.NomeTarefaBloqueio}\"")
                {
                    UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true,
                };
                using var p = Process.Start(psi)!;
                p.WaitForExit(10000);
                if (p.ExitCode != 0)
                {
                    var saida = (p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd()).Trim();
                    logger.LogError("Falha ao disparar a tarefa de bloqueio (schtasks {Code}): {Saida}", p.ExitCode, saida);
                    return false;
                }
            }
            else
            {
                var exe = Environment.ProcessPath!;
                Process.Start(new ProcessStartInfo(exe, "--overlay") { UseShellExecute = false });
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
}
