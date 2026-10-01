using Tracker.Domain.Enums;

namespace Tracker.Application.Relatorios;

/// <summary>
/// Regra de origem do ativo: alugado x próprio. Nos tipos com agente e periféricos não existe campo
/// no cadastro — a TI marca o equipamento alugado pelo PREFIXO da identificação (ver CLAUDE.md).
/// A exceção é a IMPRESSORA, que tem o interruptor <c>Alugada</c> no perfil. O espelho desta regra no
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

    /// <summary>
    /// Regra completa, por tipo: a IMPRESSORA não segue o prefixo — vale o interruptor manual
    /// <c>Impressora.Alugada</c> (<paramref name="impressoraAlugada"/>); os demais tipos seguem
    /// o prefixo "MAQ" da identificação exibida. Uma impressora com identificação "MAQ..." e o
    /// interruptor desligado é PRÓPRIA.
    /// </summary>
    public static bool EhAlugado(TipoDispositivo tipo, bool? impressoraAlugada, string? identificacaoExibida) =>
        tipo == TipoDispositivo.Impressora
            ? impressoraAlugada == true
            : EhAlugado(identificacaoExibida);

    /// <summary>Rótulo pt-BR usado nas colunas de relatório.</summary>
    public static string Rotulo(bool alugado) => alugado ? "Alugado" : "Próprio";
}
