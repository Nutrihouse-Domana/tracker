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

    /// <summary>Tipo do dispositivo para o auto-cadastro: "Notebook" ou "Mobile" (valores de
    /// TipoDispositivo aceitos pelo auto-cadastro; "Telefone" deixou de existir).</summary>
    public string Tipo { get; set; } = "Notebook";

    /// <summary>Intervalo do heartbeat (status, comandos pendentes, estado de bloqueio).</summary>
    public int HeartbeatIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Intervalo mínimo entre consultas de localização. Fica separado do heartbeat de propósito:
    /// o ip-api.com gratuito aceita 45 consultas/min POR IP, e os notebooks do escritório saem
    /// pelo mesmo IP — consultar a cada heartbeat de 30 s estouraria com mais de 22 máquinas. Também
    /// segura o histórico de localização gravado no servidor (LGPD). Entre consultas, o heartbeat
    /// vai sem localização e o servidor não grava ponto novo.
    /// </summary>
    public int LocalizacaoIntervalSeconds { get; set; } = 300;

    /// <summary>
    /// Se true, não trava a tela de verdade — apenas registra no log o que faria.
    /// Use true para testes seguros; false na VM/máquina de produção.
    /// </summary>
    public bool Simular { get; set; } = true;
}
