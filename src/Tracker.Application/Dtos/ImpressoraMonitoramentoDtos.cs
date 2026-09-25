using Tracker.Domain.Enums;

namespace Tracker.Application.Dtos;

/// <summary>
/// Um suprimento (toner, tinta, cilindro, fusor...) lido por SNMP. É também o formato gravado em
/// <c>Impressora.SuprimentosJson</c>/<c>LeituraImpressora.SuprimentosJson</c>
/// (<c>[{descricao, cor, percentual}]</c>). <paramref name="Percentual"/> é nulo quando a
/// impressora não informa o nível (valores especiais -1/-2/-3 da Printer-MIB).
/// </summary>
public record SuprimentoImpressoraDto(string Descricao, string? Cor, int? Percentual);

/// <summary>Último estado de monitoramento de uma impressora (GET /api/impressoras/{id}/monitoramento).</summary>
/// <param name="Monitoravel">
/// True quando o servidor consegue consultá-la: monitoramento ligado, conexão de rede e endereço
/// preenchido. Uma impressora que deixou de ser monitorável continua com o último estado e o
/// histórico visíveis (serve para conferir a franquia de um período antigo).
/// </param>
/// <param name="LimiteSuprimentoPercentual">Limite configurado para o alerta de suprimento baixo (a tela pinta de vermelho).</param>
public record MonitoramentoImpressoraDto(
    Guid DispositivoId,
    bool MonitorarSnmp,
    ConexaoImpressora Conexao,
    string? EnderecoRede,
    bool Monitoravel,
    DateTime? UltimaLeituraEm,
    DateTime? UltimaRespostaEm,
    bool? Online,
    long? ContadorPaginas,
    string? EstadoReportado,
    string? ErroUltimaLeitura,
    IReadOnlyList<SuprimentoImpressoraDto> Suprimentos,
    int LimiteSuprimentoPercentual);

/// <summary>Uma consulta SNMP do histórico. <paramref name="LidaEm"/> é o <c>CriadoEm</c> da leitura (UTC).</summary>
public record LeituraImpressoraDto(
    Guid Id,
    DateTime LidaEm,
    bool Online,
    long? ContadorPaginas,
    string? EstadoReportado,
    IReadOnlyList<SuprimentoImpressoraDto> Suprimentos,
    string? Erro);

/// <summary>
/// Leituras de um período (GET /api/impressoras/{id}/leituras). <paramref name="De"/>/<paramref name="Ate"/>
/// são dias civis de Brasília, ambos inclusivos. <paramref name="PaginasNoPeriodo"/> = maior contador
/// − menor contador válido do período (nulo com menos de duas leituras com contador): é o número que
/// a TI confere contra a franquia da locação.
/// </summary>
public record LeiturasImpressoraDto(
    Guid DispositivoId,
    DateOnly De,
    DateOnly Ate,
    long? PaginasNoPeriodo,
    long? ContadorInicial,
    long? ContadorFinal,
    int TotalLeituras,
    IReadOnlyList<LeituraImpressoraDto> Leituras);
