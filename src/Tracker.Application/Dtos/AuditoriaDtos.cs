using Tracker.Domain.Enums;

namespace Tracker.Application.Dtos;

public record LogAuditoriaDto(
    Guid Id,
    DateTime DataHora,
    Guid? AdmId,
    string AdmNome,
    AcaoAuditoria Acao,
    string? EntidadeTipo,
    Guid? EntidadeId,
    string Descricao,
    string? Detalhes,
    string? EnderecoIp);

/// <summary>Página de resultados da trilha de auditoria.</summary>
public record PaginaAuditoriaDto(
    IReadOnlyList<LogAuditoriaDto> Itens,
    int Total,
    int Pagina,
    int TamanhoPagina);

/// <summary>Valor + rótulo de um tipo de ação, para o filtro da tela.</summary>
public record AcaoAuditoriaDto(AcaoAuditoria Valor, string Rotulo);
