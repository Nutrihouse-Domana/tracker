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

    /// <summary>
    /// Identificação interna definida MANUALMENTE pela TI (um apelido/rótulo próprio do inventário).
    /// Diferente do <see cref="DispositivoComAgente.Hostname"/>, que o agente reporta e sobrescreve a
    /// cada heartbeat: este campo é da TI e nunca é tocado pelo agente. Quando preenchido, é ele que
    /// aparece como a identificação do dispositivo nas telas; nulo = usa hostname/número de série.
    /// </summary>
    public string? IdentificacaoInterna { get; set; }
    public StatusDispositivo Status { get; set; } = StatusDispositivo.EmEstoque;
    public DateTime DataAquisicao { get; set; }

    /// <summary>
    /// Data em que o CONTRATO DE ALUGUEL do equipamento vence. É <c>DateOnly</c> de propósito:
    /// data civil não tem hora nem fuso. Como <c>DateTime</c>, o conversor de JSON da API
    /// (que carimba todo DateTime com 'Z') fazia o navegador em UTC-3 exibir o dia ANTERIOR.
    /// Nulo = sem contrato registrado, que é o caso dos ativos próprios. O campo existe em todos os
    /// tipos porque a TI pode alugar qualquer um deles; quem indica que o ativo é alugado continua
    /// sendo o prefixo da identificação (ver CLAUDE.md), não este campo.
    /// </summary>
    public DateOnly? VencimentoAluguel { get; set; }
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
    /// <summary>
    /// versionCode do APK instalado, reportado no heartbeat. Só o agente Android envia (nulo no
    /// Windows) — é o que o servidor compara para oferecer a atualização OTA.
    /// </summary>
    public int? AgenteVersaoCode { get; set; }
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

/// <summary>
/// Impressora. NÃO tem agente (não é possível instalar software nelas): o inventário é manual e,
/// quando está em rede com <see cref="MonitorarSnmp"/> ligado, o SERVIDOR consulta o status por
/// SNMP (Printer-MIB) e grava o último estado aqui e o histórico em <see cref="Leituras"/>.
/// </summary>
public class Impressora : Dispositivo
{
    public Impressora()
    {
        Tipo = TipoDispositivo.Impressora;
        // Impressora é do LOCAL (unidade), não de um detentor nominal.
        PertenceAUsuario = false;
    }

    public TipoImpressora TipoImpressora { get; set; }
    public string? Fabricante { get; set; }
    public string? Modelo { get; set; }
    public bool Colorida { get; set; }

    /// <summary>
    /// Interruptor MANUAL de aluguel. As impressoras não seguem a convenção do prefixo "MAQ" na
    /// identificação (que vale para os outros tipos): para elas, quem diz se é alugada é este campo.
    /// Espelhos da regra: <c>ClassificacaoAluguel</c> (backend) e <c>aluguel.ts</c> (site).
    /// </summary>
    public bool Alugada { get; set; }

    public ConexaoImpressora Conexao { get; set; } = ConexaoImpressora.Rede;
    /// <summary>IP ou nome DNS. Nulo nas ligadas por USB.</summary>
    public string? EnderecoRede { get; set; }
    public string? MacAddress { get; set; }

    /// <summary>Liga a consulta SNMP periódica. Só tem efeito com Conexao=Rede e EnderecoRede preenchido.</summary>
    public bool MonitorarSnmp { get; set; }

    // --- Último estado lido por SNMP (nulo enquanto nunca foi lida) ---

    /// <summary>Quando o servidor tentou ler pela última vez (com ou sem sucesso).</summary>
    public DateTime? UltimaLeituraEm { get; set; }
    /// <summary>Última vez que a impressora RESPONDEU.</summary>
    public DateTime? UltimaRespostaEm { get; set; }
    /// <summary>Resultado da última tentativa. Nulo = nunca lida.</summary>
    public bool? Online { get; set; }
    /// <summary>Contador total de páginas (prtMarkerLifeCount).</summary>
    public long? ContadorPaginas { get; set; }
    /// <summary>Estado reportado pela impressora (hrPrinterStatus / hrDeviceStatus) em texto pt-BR.</summary>
    public string? EstadoReportado { get; set; }
    /// <summary>Suprimentos da última leitura, JSON de <c>[{descricao, cor, percentual}]</c>.</summary>
    public string? SuprimentosJson { get; set; }
    /// <summary>Erro da última tentativa (timeout, community recusada...). Nulo quando respondeu.</summary>
    public string? ErroUltimaLeitura { get; set; }

    public ICollection<LeituraImpressora> Leituras { get; set; } = new List<LeituraImpressora>();
}
