namespace Tracker.Application.Relatorios;

/// <summary>Relatórios disponíveis. O valor viaja na rota (<c>/api/relatorios/{tipo}/pdf</c>).</summary>
public enum TipoRelatorio
{
    /// <summary>Todos os ativos em inventário, com identificação, origem, unidade, responsável e status.</summary>
    Inventario,
    /// <summary>Alugados x próprios: totais por origem e a lista dos alugados.</summary>
    Aluguel,
    /// <summary>Distribuição do parque por unidade, com quebra por status.</summary>
    Unidades,
}

/// <summary>Uma linha de ativo no relatório — já resolvida (sem navegações do EF).</summary>
/// <param name="CadastradoEm">
/// Quando o ativo entrou no Tracker (<c>EntidadeBase.CriadoEm</c>), já em horário local. NÃO é a
/// data de aquisição do equipamento, que é um campo separado preenchido pela TI no cadastro.
/// </param>
public record LinhaInventarioDto(
    string Identificacao,
    string Tipo,
    string Origem,
    string NumeroSerie,
    string? Patrimonio,
    string? Unidade,
    string? Responsavel,
    string Status,
    DateTime CadastradoEm,
    /// <summary>Vencimento do contrato de aluguel; nulo quando o ativo não tem contrato registrado.</summary>
    DateOnly? VencimentoAluguel = null);

/// <summary>Contagem de uma fatia do parque (por tipo, status, unidade ou origem).</summary>
public record ContagemDto(string Rotulo, int Quantidade);

/// <summary>Uma unidade com o total de ativos e a quebra por status.</summary>
public record LinhaUnidadeDto(string Unidade, int Total, int Alugados, IReadOnlyList<ContagemDto> PorStatus);

/// <summary>
/// Números que alimentam os cards da tela de Perfil — o mesmo conteúdo que os arquivos exportados
/// resumem, para o ADM conferir antes de baixar.
/// </summary>
public record ResumoRelatoriosDto(
    int TotalAtivos,
    int Alugados,
    int Proprios,
    int Unidades,
    IReadOnlyList<ContagemDto> PorTipo,
    IReadOnlyList<ContagemDto> PorStatus,
    DateTime GeradoEm);
