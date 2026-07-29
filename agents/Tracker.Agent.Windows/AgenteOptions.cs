namespace Tracker.Agent.Windows;

/// <summary>Configuração do agente (appsettings.json, seção "Agente").</summary>
public class AgenteOptions
{
    /// <summary>URL base do backend, ex.: https://tracker.nutrihouse.local</summary>
    public string BackendBaseUrl { get; set; } = "http://localhost:5171";

    /// <summary>
    /// Token individual do dispositivo. Pode ser preenchido manualmente pela TI, OU deixado vazio
    /// para o agente obtê-lo sozinho via auto-cadastro (usando EnrollmentKey). Uma vez obtido, é
    /// salvo em token.dat ao lado do executável.
    /// </summary>
    public string AgentToken { get; set; } = string.Empty;

    /// <summary>Chave única da empresa para auto-cadastro. Igual em todas as instalações.</summary>
    public string EnrollmentKey { get; set; } = string.Empty;

    /// <summary>Tipo do dispositivo para o auto-cadastro: "Notebook" ou "Telefone".</summary>
    public string Tipo { get; set; } = "Notebook";

    public int HeartbeatIntervalSeconds { get; set; } = 60;

    /// <summary>
    /// Se true, não trava a tela de verdade — apenas registra no log o que faria.
    /// Use true para testes seguros; false na VM/máquina de produção.
    /// </summary>
    public bool Simular { get; set; } = true;
}
