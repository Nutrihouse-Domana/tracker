using Tracker.Domain.Common;
using Tracker.Domain.Enums;

namespace Tracker.Domain.Entities;

/// <summary>
/// Evento reportado pelo próprio agente, sem comando da TI. Existe porque no Android o usuário
/// pode desativar a permissão de administrador: sem esse registro a TI não teria como saber que
/// um aparelho deixou de estar protegido.
/// </summary>
public class AlertaDispositivo : EntidadeBase
{
    public Guid DispositivoId { get; set; }
    public Dispositivo? Dispositivo { get; set; }

    public TipoAlertaDispositivo Tipo { get; set; }

    /// <summary>Texto livre do agente com o contexto do evento (ex.: pacote que pediu a desativação).</summary>
    public string? Detalhe { get; set; }

    /// <summary>Marcado pela TI depois de tratar o alerta, para sair da lista de pendências.</summary>
    public bool Reconhecido { get; set; }
    public DateTime? ReconhecidoEm { get; set; }
    public Guid? ReconhecidoPorAdmId { get; set; }
}
