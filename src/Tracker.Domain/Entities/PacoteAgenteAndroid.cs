using Tracker.Domain.Common;

namespace Tracker.Domain.Entities;

/// <summary>
/// APK do agente Android publicado pela TI para atualização OTA (sem loja). O agente compara o
/// próprio versionCode com o maior VersaoCode publicado e se atualiza sozinho quando estiver atrás.
/// O arquivo fica em disco (App_Data/pacotes-agente); aqui só os metadados e o hash de integridade.
/// </summary>
public class PacoteAgenteAndroid : EntidadeBase
{
    /// <summary>versionCode do APK — o número que o Android compara para permitir a atualização.</summary>
    public int VersaoCode { get; set; }

    /// <summary>versionName do APK (ex.: "1.1.0"), exibido no painel.</summary>
    public string VersaoNome { get; set; } = string.Empty;

    public string NomeArquivoOriginal { get; set; } = string.Empty;

    /// <summary>Caminho relativo à raiz de pacotes (Storage:PacotesAgentePath).</summary>
    public string CaminhoRelativo { get; set; } = string.Empty;

    /// <summary>SHA-256 (hex) do APK. O agente confere antes de instalar o que baixou.</summary>
    public string HashSha256 { get; set; } = string.Empty;

    public long TamanhoBytes { get; set; }

    /// <summary>ADM que publicou o pacote (confirmado por senha).</summary>
    public Guid EnviadoPorAdmId { get; set; }
}
