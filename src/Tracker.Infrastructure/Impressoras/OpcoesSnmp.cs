namespace Tracker.Infrastructure.Impressoras;

/// <summary>
/// Seção <c>Snmp</c> da configuração. Em produção a community vem da variável de ambiente
/// <c>Snmp__Community</c> (ver docs/operacao/monitoramento-impressoras-snmp.md).
/// </summary>
public class OpcoesSnmp
{
    public const string Secao = "Snmp";

    /// <summary>Liga o ciclo periódico. Desligado, "Ler agora" continua funcionando.</summary>
    public bool Habilitado { get; set; } = true;
    public int IntervaloMinutos { get; set; } = 15;
    public string Community { get; set; } = "public";
    /// <summary>Timeout de CADA requisição UDP (GET ou passo do WALK).</summary>
    public int TimeoutMs { get; set; } = 3000;
    public int Porta { get; set; } = 161;
    public int MaxParalelismo { get; set; } = 8;
    public int LimiteSuprimentoPercentual { get; set; } = 15;
    /// <summary>Falhas SEGUIDAS até gerar o alerta de impressora offline.</summary>
    public int FalhasParaOffline { get; set; } = 2;
    /// <summary>Leituras mais velhas que isto são apagadas (poda diária).</summary>
    public int RetencaoDias { get; set; } = 730;
    /// <summary>Sem resposta em v2c, tenta de novo em v1 (impressoras antigas). Custa um timeout a mais nas offline.</summary>
    public bool TentarV1 { get; set; } = true;
}
