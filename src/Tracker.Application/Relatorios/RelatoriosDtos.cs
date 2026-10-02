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
    /// <summary>Notebooks, mobiles e periféricos com a situação do termo de uso do detentor atual.</summary>
    Termos,
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

/// <summary>Situação do termo de uso de um equipamento em relação ao detentor ATUAL.</summary>
public enum SituacaoTermo
{
    /// <summary>O detentor atual tem termo assinado registrado para este equipamento.</summary>
    ComTermo,
    /// <summary>Tem detentor, mas nenhum termo dele foi registrado para este equipamento.</summary>
    SemTermo,
    /// <summary>Sem detentor (estoque ou ativo do local): termo não se aplica.</summary>
    NaoSeAplica,
}

/// <summary>Rótulo da situação do termo — o mesmo no PDF e no Excel.</summary>
public static class SituacaoTermoExtensions
{
    public static string Rotulo(this SituacaoTermo situacao) => situacao switch
    {
        SituacaoTermo.ComTermo => "Com termo",
        SituacaoTermo.SemTermo => "Sem termo",
        _ => "Não se aplica",
    };
}

/// <summary>Um equipamento no relatório de termos de uso.</summary>
/// <param name="DataAceite">Data do termo do detentor atual, já no horário de Brasília; nulo sem termo.</param>
/// <param name="TermoAnteriorDe">
/// Quando o detentor atual NÃO tem termo mas o equipamento tem termo de outra pessoa (detentor
/// anterior), o nome dela — sinal de que o equipamento trocou de mão sem termo novo.
/// </param>
public record LinhaTermoDto(
    string Identificacao,
    string Tipo,
    string NumeroSerie,
    string? Patrimonio,
    string? Unidade,
    string? Detentor,
    string Status,
    SituacaoTermo Situacao,
    string? VersaoTermo,
    DateTime? DataAceite,
    string? TermoAnteriorDe);

/// <summary>Totais do relatório de termos (equipamentos de detentor nominal, sem impressoras).</summary>
public record ResumoTermosDto(int Equipamentos, int ComTermo, int SemTermo, int SemDetentor, DateTime GeradoEm);
