using Tracker.Domain.Common;
using Tracker.Domain.Enums;

namespace Tracker.Domain.Entities;

/// <summary>
/// Trilha de auditoria unificada: um registro por alteração feita por um Usuario ADM (quem, o quê,
/// quando). <c>CriadoEm</c> (da base) é a data/hora do evento em UTC.
/// </summary>
/// <remarks>
/// <see cref="EntidadeId"/> é um Guid solto, SEM chave estrangeira, de propósito: o log tem de
/// sobreviver à exclusão definitiva do alvo (o dispositivo é apagado, mas o registro "excluiu o
/// dispositivo X" precisa ficar). O nome do ADM é gravado em <see cref="AdmNome"/> como fotografia,
/// pelo mesmo motivo — o log não pode depender de o login ainda existir/ter o mesmo nome depois.
/// </remarks>
public class LogAuditoria : EntidadeBase
{
    /// <summary>Login que executou a ação. Nulo para ações de sistema (ex.: seed) ou do agente.</summary>
    public Guid? AdmId { get; set; }

    /// <summary>Nome do ADM no momento da ação (fotografia — não é resolvido por join).</summary>
    public string AdmNome { get; set; } = string.Empty;

    public AcaoAuditoria Acao { get; set; }

    /// <summary>Tipo do alvo (ex.: "Dispositivo", "Unidade", "Acesso"), para filtrar a trilha.</summary>
    public string? EntidadeTipo { get; set; }

    /// <summary>Id do alvo (Guid solto, sem FK — ver remarks).</summary>
    public Guid? EntidadeId { get; set; }

    /// <summary>Resumo legível em pt-BR (ex.: "Bloqueou o notebook NB-014").</summary>
    public string Descricao { get; set; } = string.Empty;

    /// <summary>Contexto extra opcional (motivo, de→para) — texto livre.</summary>
    public string? Detalhes { get; set; }

    /// <summary>IP de origem da requisição, quando disponível.</summary>
    public string? EnderecoIp { get; set; }
}
