using Tracker.Domain.Common;
using Tracker.Domain.Enums;

namespace Tracker.Domain.Entities;

/// <summary>
/// Base TPH para Notebook, Telefone e Periferico. Notebook/Telefone têm agente instalado
/// (Hostname, heartbeat, bloqueio); Periferico é inventário manual, sem agente.
/// </summary>
public abstract class Dispositivo : EntidadeBase
{
    public TipoDispositivo Tipo { get; protected set; }
    public string NumeroSerie { get; set; } = string.Empty;
    public string AssetTag { get; set; } = string.Empty;
    public StatusDispositivo Status { get; set; } = StatusDispositivo.EmEstoque;
    public DateTime DataAquisicao { get; set; }
    public string? Observacoes { get; set; }

    public Guid? UsuarioAtualId { get; set; }
    public Usuario? UsuarioAtual { get; set; }

    public ICollection<HistoricoMovimentacao> HistoricoMovimentacoes { get; set; } = new List<HistoricoMovimentacao>();
    public ICollection<HistoricoLocalizacao> HistoricoLocalizacoes { get; set; } = new List<HistoricoLocalizacao>();
    public ICollection<HistoricoReparo> HistoricoReparos { get; set; } = new List<HistoricoReparo>();
    public ICollection<ComentarioDispositivo> Comentarios { get; set; } = new List<ComentarioDispositivo>();
}

/// <summary>Campos compartilhados por Notebook e Telefone: dispositivos com agente instalado.</summary>
public abstract class DispositivoComAgente : Dispositivo
{
    public string Hostname { get; set; } = string.Empty;
    public string SistemaOperacional { get; set; } = string.Empty;
    public string? VersaoSO { get; set; }
    public string? MacAddress { get; set; }
    public string? AgenteVersao { get; set; }
    public DateTime? UltimoHeartbeatEm { get; set; }
    public bool GeofenceAtivo { get; set; }
    public StatusComandoRemoto? StatusUltimoComando { get; set; }

    /// <summary>Token/API key emitido no enrollment do agente. Armazenar apenas o hash.</summary>
    public string? TokenAgenteHash { get; set; }
}

public class Notebook : DispositivoComAgente
{
    public Notebook() => Tipo = TipoDispositivo.Notebook;
}

public class Telefone : DispositivoComAgente
{
    public Telefone() => Tipo = TipoDispositivo.Telefone;

    public string Imei { get; set; } = string.Empty;
}

public class Periferico : Dispositivo
{
    public Periferico() => Tipo = TipoDispositivo.Periferico;

    public TipoPeriferico TipoPeriferico { get; set; }
}
