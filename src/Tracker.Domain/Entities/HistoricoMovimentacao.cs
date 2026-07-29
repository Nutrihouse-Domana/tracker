using Tracker.Domain.Common;

namespace Tracker.Domain.Entities;

/// <summary>Registro de troca de posse e/ou local de um dispositivo.</summary>
public class HistoricoMovimentacao : EntidadeBase
{
    public Guid DispositivoId { get; set; }
    public Dispositivo? Dispositivo { get; set; }

    public Guid? UsuarioOrigemId { get; set; }
    public Usuario? UsuarioOrigem { get; set; }

    public Guid? UsuarioDestinoId { get; set; }
    public Usuario? UsuarioDestino { get; set; }

    public Guid? LocalOrigemId { get; set; }
    public Local? LocalOrigem { get; set; }

    public Guid? LocalDestinoId { get; set; }
    public Local? LocalDestino { get; set; }

    public DateTime DataMovimentacao { get; set; } = DateTime.UtcNow;
    public string? Motivo { get; set; }

    /// <summary>Usuario ADM que registrou a movimentação.</summary>
    public Guid RegistradoPorAdmId { get; set; }
}
