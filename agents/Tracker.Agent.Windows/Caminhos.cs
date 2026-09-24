namespace Tracker.Agent.Windows;

/// <summary>Caminhos e nomes compartilhados entre o serviço e a tela de bloqueio.</summary>
public static class Caminhos
{
    public static readonly string PastaDados = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "TrackerAgent");

    /// <summary>Arquivo-sinal: enquanto existe, o dispositivo está bloqueado. Escrito/removido pelo serviço.
    /// A pasta é somente leitura para usuários: a tela de bloqueio só LÊ o sinal.</summary>
    public static readonly string FlagBloqueio = Path.Combine(PastaDados, "locked.flag");

    /// <summary>Tarefa agendada que lança a tela de bloqueio na sessão interativa do usuário.</summary>
    public const string NomeTarefaBloqueio = "TrackerAgentLock";
}
