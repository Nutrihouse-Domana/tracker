namespace Tracker.Agent.Windows;

/// <summary>Caminhos e nomes compartilhados entre o serviço e a tela de bloqueio.</summary>
public static class Caminhos
{
    public static readonly string PastaDados = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "TrackerAgent");

    /// <summary>Arquivo-sinal: enquanto existe, o dispositivo está bloqueado. Escrito/removido pelo serviço.</summary>
    public static readonly string FlagBloqueio = Path.Combine(PastaDados, "locked.flag");

    /// <summary>Marcador "vivo" que a tela de bloqueio atualiza enquanto está na tela (heartbeat de UI).
    /// O serviço usa para saber se já existe uma tela aberta antes de lançar outra.</summary>
    public static readonly string MarcadorOverlay = Path.Combine(PastaDados, "overlay-alive.flag");

    /// <summary>Tarefa agendada que lança a tela de bloqueio na sessão interativa do usuário.</summary>
    public const string NomeTarefaBloqueio = "TrackerAgentLock";
}
