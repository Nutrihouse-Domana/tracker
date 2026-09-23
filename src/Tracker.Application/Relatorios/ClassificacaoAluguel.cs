namespace Tracker.Application.Relatorios;

/// <summary>
/// Regra de origem do ativo: alugado x próprio. Não existe campo no cadastro — a TI marca o
/// equipamento alugado pelo PREFIXO da identificação (ver CLAUDE.md). O espelho desta regra no
/// site é <c>src/Tracker.Web/src/features/dispositivos/aluguel.ts</c>: ao mudar o critério,
/// mude nos DOIS lados, senão a coluna da tela e o relatório passam a discordar.
/// </summary>
public static class ClassificacaoAluguel
{
    /// <summary>Prefixo que marca equipamento alugado.</summary>
    public const string PrefixoAlugado = "MAQ";

    /// <summary>
    /// Identificação exibida do ativo, na mesma ordem de precedência das telas:
    /// rótulo manual da TI, senão hostname (reportado pelo agente), senão número de série.
    /// </summary>
    public static string IdentificacaoExibida(string? identificacaoInterna, string? hostname, string? numeroSerie) =>
        !string.IsNullOrWhiteSpace(identificacaoInterna) ? identificacaoInterna
        : !string.IsNullOrWhiteSpace(hostname) ? hostname
        : numeroSerie ?? string.Empty;

    /// <summary>True quando a identificação exibida começa com o prefixo de alugado.</summary>
    public static bool EhAlugado(string? identificacaoExibida) =>
        (identificacaoExibida ?? string.Empty).TrimStart()
            .StartsWith(PrefixoAlugado, StringComparison.OrdinalIgnoreCase);

    /// <summary>Rótulo pt-BR usado nas colunas de relatório.</summary>
    public static string Rotulo(bool alugado) => alugado ? "Alugado" : "Próprio";
}
