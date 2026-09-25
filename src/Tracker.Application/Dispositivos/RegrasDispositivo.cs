using Tracker.Domain.Entities;
using Tracker.Domain.Enums;

namespace Tracker.Application.Dispositivos;

/// <summary>
/// Regras de status do dispositivo que mais de um endpoint aplica (heartbeat, confirmação de
/// comando, posse, exclusão). Ficam aqui, puras, para os controllers não divergirem entre si e
/// para serem testáveis sem banco.
/// </summary>
public static class RegrasDispositivo
{
    /// <summary>
    /// "Em uso" exige um responsável: o detentor, quando o ativo pertence a usuário, ou a unidade,
    /// quando pertence ao local.
    /// </summary>
    public static bool TemResponsavel(Dispositivo dispositivo) =>
        dispositivo.PertenceAUsuario
            ? dispositivo.UsuarioAtualId is not null
            : dispositivo.UnidadeId is not null;

    /// <summary>
    /// Estado de bloqueio que o servidor manda o agente manter. Vem do último comando emitido,
    /// e NÃO só do <see cref="StatusDispositivo"/>: o status só vira Bloqueado quando o agente
    /// confirma, então o heartbeat que entrega um Bloquear pendente responderia "desbloqueado" e o
    /// agente Android destravaria logo depois de aplicar. Sem comando algum, vale o status.
    /// </summary>
    /// <param name="status">Status atual do dispositivo.</param>
    /// <param name="ultimoComando">
    /// O comando de bloqueio/desbloqueio mais recente que não falhou (pendente, enviado ou
    /// confirmado) — ver <c>ConsultasComandoRemoto.UltimoComandoVigenteAsync</c> na API.
    /// </param>
    public static bool DeveEstarBloqueado(StatusDispositivo status, ComandoRemoto? ultimoComando) =>
        ultimoComando is not null
            ? ultimoComando.TipoComando == TipoComandoRemoto.Bloquear
            : status == StatusDispositivo.Bloqueado;

    /// <summary>
    /// Bloqueio emitido e ainda não confirmado pelo agente (o mais recente é um Bloquear pendente
    /// ou enviado). O status ainda não diz "Bloqueado", mas o aparelho vai travar assim que receber.
    /// </summary>
    public static bool TemBloqueioAChegar(ComandoRemoto? ultimoComando) =>
        ultimoComando is { TipoComando: TipoComandoRemoto.Bloquear } &&
        ultimoComando.Status is StatusComandoRemoto.Pendente or StatusComandoRemoto.Enviado;

    /// <summary>
    /// Status depois de o agente confirmar o desbloqueio. Só sai de Bloqueado — Baixado e Em
    /// manutenção são decisões da TI que o desbloqueio não desfaz. Volta a "Em uso" apenas se há
    /// responsável (ver <see cref="TemResponsavel"/>); senão, estoque.
    /// </summary>
    public static StatusDispositivo StatusAposDesbloqueio(Dispositivo dispositivo)
    {
        if (dispositivo.Status != StatusDispositivo.Bloqueado) return dispositivo.Status;
        return TemResponsavel(dispositivo) ? StatusDispositivo.EmUso : StatusDispositivo.EmEstoque;
    }

    /// <summary>Mensagem única das recusas de detentor/termo para impressora (400 nos endpoints).</summary>
    public const string MensagemImpressoraSemDetentor =
        "Impressora pertence ao local (unidade): não pode ser atribuída a um usuário nem ter termo de uso.";

    /// <summary>
    /// Se o tipo admite detentor nominal (atribuição, "pertence a usuário", termo de uso).
    /// Impressora é sempre do local — o espelho no site esconde os mesmos controles.
    /// </summary>
    public static bool AceitaDetentor(Dispositivo dispositivo) => dispositivo is not Impressora;

    /// <summary>
    /// Grava a conexão da impressora já normalizada: textos aparados (vazio vira nulo) e, com USB,
    /// endereço nulo e SNMP desligado — o servidor não alcança impressora ligada a um computador.
    /// O validador já recusa USB com SNMP ligado; isto é a segunda barreira (e a que vale se um
    /// caminho novo esquecer o validador).
    /// </summary>
    public static void AplicarConexao(
        Impressora impressora, ConexaoImpressora conexao, string? enderecoRede, string? macAddress, bool monitorarSnmp)
    {
        static string? Limpo(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

        impressora.Conexao = conexao;
        impressora.MacAddress = Limpo(macAddress);

        if (conexao == ConexaoImpressora.Usb)
        {
            impressora.EnderecoRede = null;
            impressora.MonitorarSnmp = false;
            return;
        }

        impressora.EnderecoRede = Limpo(enderecoRede);
        // Sem endereço não há o que consultar: o SNMP só fica ligado com IP/nome preenchido.
        impressora.MonitorarSnmp = monitorarSnmp && impressora.EnderecoRede is not null;
    }

    /// <summary>
    /// Corrige um "Em uso" que ficou sem responsável (ex.: troca de posse). Outros status —
    /// inclusive Bloqueado — não são tocados.
    /// </summary>
    public static StatusDispositivo StatusCoerenteComResponsavel(Dispositivo dispositivo) =>
        dispositivo.Status == StatusDispositivo.EmUso && !TemResponsavel(dispositivo)
            ? StatusDispositivo.EmEstoque
            : dispositivo.Status;
}
