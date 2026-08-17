using Tracker.Domain.Common;
using Tracker.Domain.Enums;

namespace Tracker.Domain.Entities;

/// <summary>
/// Base TPH para Notebook, Mobile e Periferico. Os dois primeiros têm agente instalado
/// (Hostname, heartbeat, bloqueio); Periferico é inventário manual, sem agente.
/// </summary>
public abstract class Dispositivo : EntidadeBase
{
    public TipoDispositivo Tipo { get; protected set; }
    public string NumeroSerie { get; set; } = string.Empty;
    /// <summary>Patrimônio. Nulo em periféricos, que não recebem etiqueta.</summary>
    public string? AssetTag { get; set; }
    public StatusDispositivo Status { get; set; } = StatusDispositivo.EmEstoque;
    public DateTime DataAquisicao { get; set; }
    public string? Observacoes { get; set; }

    public Guid? UsuarioAtualId { get; set; }
    public Usuario? UsuarioAtual { get; set; }

    /// <summary>Unidade que o dispositivo atende (matriz, filial...). Nulo enquanto não definida.</summary>
    public Guid? UnidadeId { get; set; }
    public Unidade? Unidade { get; set; }

    /// <summary>
    /// True: o ativo é de um usuário nominal (fluxo normal de atribuição e termo de uso).
    /// False: é do local — fica lotado na <see cref="Unidade"/> e não tem detentor individual
    /// (ex.: notebook de sala de reunião). Nesse caso <see cref="UsuarioAtualId"/> é sempre nulo.
    /// </summary>
    public bool PertenceAUsuario { get; set; } = true;

    // --- Arquivamento (exclusão lógica) ---
    // Excluir de verdade destruiria movimentações, reparos e termos de uso assinados, que são a
    // razão de o Tracker existir. A "exclusão" some com o ativo de todas as telas via filtro
    // global de consulta, preservando a trilha de auditoria e permitindo restaurar.

    /// <summary>Quando o ativo foi arquivado. Nulo = ativo normal.</summary>
    public DateTime? ExcluidoEm { get; set; }
    /// <summary>ADM que confirmou a exclusão com a própria senha.</summary>
    public Guid? ExcluidoPorAdmId { get; set; }
    public string? MotivoExclusao { get; set; }

    public ICollection<HistoricoMovimentacao> HistoricoMovimentacoes { get; set; } = new List<HistoricoMovimentacao>();
    public ICollection<HistoricoLocalizacao> HistoricoLocalizacoes { get; set; } = new List<HistoricoLocalizacao>();
    public ICollection<HistoricoReparo> HistoricoReparos { get; set; } = new List<HistoricoReparo>();
    public ICollection<ComentarioDispositivo> Comentarios { get; set; } = new List<ComentarioDispositivo>();
    public ICollection<AlertaDispositivo> Alertas { get; set; } = new List<AlertaDispositivo>();
}

/// <summary>Campos compartilhados por Notebook, Telefone e Tablet: dispositivos com agente instalado.</summary>
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

    // --- Telemetria reportada pelo agente móvel (nula em notebooks Windows) ---

    /// <summary>Fabricante informado pelo aparelho (ex.: Samsung). Preenchido pelo agente Android.</summary>
    public string? Fabricante { get; set; }
    /// <summary>Modelo comercial do aparelho (ex.: SM-X200). Preenchido pelo agente Android.</summary>
    public string? Modelo { get; set; }
    /// <summary>Carga da bateria em porcentagem no último heartbeat.</summary>
    public int? BateriaPercentual { get; set; }

    /// <summary>
    /// Proteção do agente contra remoção pelo usuário. Nulo no Windows (roda como Serviço).
    /// Serve para a TI ver na lista quais aparelhos estão realmente protegidos.
    /// </summary>
    public NivelProtecaoAgente? NivelProtecao { get; set; }
}

public class Notebook : DispositivoComAgente
{
    public Notebook() => Tipo = TipoDispositivo.Notebook;
}

/// <summary>
/// Celular ou tablet Android. Um único tipo para os dois: o agente é o mesmo, e distinguir
/// aparelhos por tamanho de tela não muda nada no controle de inventário.
/// </summary>
public class Mobile : DispositivoComAgente
{
    public Mobile() => Tipo = TipoDispositivo.Mobile;

    /// <summary>IMEI. Opcional porque tablets só-Wi-Fi não têm modem celular.</summary>
    public string? Imei { get; set; }
}

public class Periferico : Dispositivo
{
    public Periferico() => Tipo = TipoDispositivo.Periferico;

    public TipoPeriferico TipoPeriferico { get; set; }
}
