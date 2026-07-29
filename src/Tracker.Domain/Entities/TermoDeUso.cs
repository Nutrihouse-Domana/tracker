using Tracker.Domain.Common;

namespace Tracker.Domain.Entities;

/// <summary>
/// Termo de uso gerado para impressão, assinado fisicamente pelo Usuario e anexado (escaneado)
/// pelo ADM ao registro do dispositivo. Não há assinatura digital remota nesta fase.
/// </summary>
public class TermoDeUso : EntidadeBase
{
    public Guid DispositivoId { get; set; }
    public Dispositivo? Dispositivo { get; set; }

    public Guid UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    public string VersaoTermo { get; set; } = string.Empty;
    public DateTime DataAceite { get; set; } = DateTime.UtcNow;

    /// <summary>SHA-256 do arquivo anexado, para checagem de integridade.</summary>
    public string HashDocumento { get; set; } = string.Empty;

    /// <summary>Caminho do PDF assinado escaneado, enviado por upload pelo ADM.</summary>
    public string CaminhoArquivoAnexado { get; set; } = string.Empty;

    public Guid RegistradoPorAdmId { get; set; }
}
