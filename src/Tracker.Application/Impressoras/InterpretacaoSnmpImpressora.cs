using System.Text;
using System.Text.Json;
using Tracker.Application.Dtos;
using Tracker.Domain.Entities;
using Tracker.Domain.Enums;

namespace Tracker.Application.Impressoras;

/// <summary>OIDs padrão (Host-MIB RFC 2790 e Printer-MIB RFC 3805) lidas de cada impressora.</summary>
public static class OidsImpressora
{
    public const string SysDescr = "1.3.6.1.2.1.1.1.0";
    public const string HrDeviceStatus = "1.3.6.1.2.1.25.3.2.1.5.1";
    public const string HrPrinterStatus = "1.3.6.1.2.1.25.3.5.1.1.1";
    /// <summary>Bits de erro detectado (sem papel, porta aberta, atolado...). OctetString.</summary>
    public const string HrPrinterDetectedErrorState = "1.3.6.1.2.1.25.3.5.1.2.1";
    /// <summary>prtMarkerLifeCount do marcador 1: contador total de páginas.</summary>
    public const string PrtMarkerLifeCount = "1.3.6.1.2.1.43.10.2.1.4.1.1";

    // Colunas da tabela prtMarkerSupplies (índice do dispositivo = 1); a folha é o índice do suprimento.
    public const string SuprimentoColoranteIndice = "1.3.6.1.2.1.43.11.1.1.3.1";
    public const string SuprimentoDescricao = "1.3.6.1.2.1.43.11.1.1.6.1";
    public const string SuprimentoMaximo = "1.3.6.1.2.1.43.11.1.1.8.1";
    public const string SuprimentoNivel = "1.3.6.1.2.1.43.11.1.1.9.1";
    /// <summary>prtMarkerColorantValue: nome da cor ("black", "cyan"...), indexado pelo colorante.</summary>
    public const string ColoranteValor = "1.3.6.1.2.1.43.12.1.1.4.1";

    /// <summary>Escalares lidos num GET só.</summary>
    public static readonly IReadOnlyList<string> Escalares =
        [SysDescr, HrDeviceStatus, HrPrinterStatus, HrPrinterDetectedErrorState, PrtMarkerLifeCount];

    /// <summary>Subárvores percorridas por WALK para montar os suprimentos.</summary>
    public static readonly IReadOnlyList<string> Tabelas =
        [SuprimentoDescricao, SuprimentoMaximo, SuprimentoNivel, SuprimentoColoranteIndice, ColoranteValor];
}

/// <summary>Resultado já interpretado de uma consulta que teve resposta.</summary>
public sealed record LeituraInterpretada(
    string? DescricaoSistema,
    long? ContadorPaginas,
    string? EstadoReportado,
    IReadOnlyList<SuprimentoImpressoraDto> Suprimentos);

/// <summary>
/// Interpretação PURA (sem rede nem banco) do que a impressora devolve por SNMP, e as regras do
/// monitoramento (quem é monitorável, alerta de offline, suprimento baixo, páginas no período).
/// </summary>
public static class InterpretacaoSnmpImpressora
{
    public const int TamanhoMaximoEstado = 200;
    public const int TamanhoMaximoErro = 500;

    private static readonly JsonSerializerOptions OpcoesJson = new(JsonSerializerDefaults.Web);

    private static readonly string[] BitsDeErro =
    [
        "pouco papel", "sem papel", "pouco toner", "sem toner", "porta aberta", "papel atolado",
        "offline", "manutenção solicitada", "bandeja de entrada ausente", "bandeja de saída ausente",
        "suprimento ausente", "bandeja de saída quase cheia", "bandeja de saída cheia",
        "bandeja de entrada vazia", "manutenção preventiva atrasada",
    ];

    private static readonly Dictionary<string, string> CoresEmPortugues = new(StringComparer.OrdinalIgnoreCase)
    {
        ["black"] = "preto",
        ["cyan"] = "ciano",
        ["magenta"] = "magenta",
        ["yellow"] = "amarelo",
        ["red"] = "vermelho",
        ["green"] = "verde",
        ["blue"] = "azul",
        ["gray"] = "cinza",
        ["grey"] = "cinza",
        ["photo black"] = "preto foto",
        ["matte black"] = "preto fosco",
    };

    /// <summary>Só entra no ciclo quem tem monitoramento ligado, está em REDE e tem endereço.</summary>
    public static bool EhMonitoravel(Impressora impressora) =>
        impressora.MonitorarSnmp
        && impressora.Conexao == ConexaoImpressora.Rede
        && !string.IsNullOrWhiteSpace(impressora.EnderecoRede);

    /// <summary>Monta a leitura a partir dos escalares (GET) e das folhas das tabelas (WALK).</summary>
    public static LeituraInterpretada Interpretar(
        IReadOnlyDictionary<string, ValorSnmp> escalares,
        IReadOnlyList<ValorSnmp> tabelas)
    {
        long? Numero(string oid) => escalares.TryGetValue(oid, out var v) ? v.Numero : null;

        var contador = Numero(OidsImpressora.PrtMarkerLifeCount);
        if (contador < 0) contador = null;

        var descricao = escalares.TryGetValue(OidsImpressora.SysDescr, out var d) ? Limpar(d.Texto) : null;
        var bitsErro = escalares.TryGetValue(OidsImpressora.HrPrinterDetectedErrorState, out var e) ? e.Bytes : null;

        return new LeituraInterpretada(
            descricao,
            contador,
            EstadoEmTexto(Numero(OidsImpressora.HrDeviceStatus), Numero(OidsImpressora.HrPrinterStatus), bitsErro),
            MontarSuprimentos(tabelas));
    }

    /// <summary>
    /// Percentual do suprimento. Nível -1 (other), -2 (unknown) e -3 ("tem algum") não são medida:
    /// devolvem nulo, assim como capacidade máxima desconhecida (≤ 0). Limitado a 0–100.
    /// </summary>
    public static int? Percentual(long? nivel, long? maximo)
    {
        if (nivel is null || maximo is null) return null;
        if (nivel < 0 || maximo <= 0) return null;
        var pct = (int)Math.Round(nivel.Value * 100.0 / maximo.Value, MidpointRounding.AwayFromZero);
        return Math.Clamp(pct, 0, 100);
    }

    /// <summary>Estado em texto pt-BR, combinando hrDeviceStatus, hrPrinterStatus e os bits de erro.</summary>
    public static string? EstadoEmTexto(long? hrDeviceStatus, long? hrPrinterStatus, byte[]? bitsErro = null)
    {
        string? principal = null;
        if (hrDeviceStatus == 5)
        {
            principal = "Parada";
        }
        else
        {
            var impressora = hrPrinterStatus switch
            {
                3 => "Ociosa",
                4 => "Imprimindo",
                5 => "Aquecendo",
                _ => null,
            };
            var dispositivo = hrDeviceStatus switch
            {
                1 => "Estado desconhecido",
                2 => "Em funcionamento",
                3 => "Com aviso",
                4 => "Em teste",
                _ => null,
            };

            if (impressora is not null)
                principal = hrDeviceStatus == 3 ? $"{impressora}, com aviso" : impressora;
            else
                principal = dispositivo ?? hrPrinterStatus switch
                {
                    1 => "Outro estado",
                    2 => "Estado desconhecido",
                    _ => null,
                };
        }

        var erros = ErrosDetectados(bitsErro);
        string? texto = (principal, erros.Count) switch
        {
            (null, 0) => null,
            (null, _) => Capitalizar(string.Join(", ", erros)),
            (_, 0) => principal,
            _ => $"{principal} — {string.Join(", ", erros)}",
        };

        return Truncar(texto, TamanhoMaximoEstado);
    }

    /// <summary>Bits de hrPrinterDetectedErrorState: o bit 0 é o MAIS significativo do primeiro byte.</summary>
    public static IReadOnlyList<string> ErrosDetectados(byte[]? bits)
    {
        var lista = new List<string>();
        if (bits is null) return lista;
        for (var n = 0; n < BitsDeErro.Length && n / 8 < bits.Length; n++)
        {
            if ((bits[n / 8] & (0x80 >> (n % 8))) != 0)
                lista.Add(BitsDeErro[n]);
        }
        return lista;
    }

    /// <summary>Texto de um OctetString: UTF-8, sem NUL/controles nem espaços nas pontas.</summary>
    public static string? TextoDeOctetos(byte[]? bytes)
    {
        if (bytes is null || bytes.Length == 0) return null;
        return Limpar(Encoding.UTF8.GetString(bytes));
    }

    private static IReadOnlyList<SuprimentoImpressoraDto> MontarSuprimentos(IReadOnlyList<ValorSnmp> tabelas)
    {
        var descricoes = Coluna(tabelas, OidsImpressora.SuprimentoDescricao);
        var maximos = Coluna(tabelas, OidsImpressora.SuprimentoMaximo);
        var niveis = Coluna(tabelas, OidsImpressora.SuprimentoNivel);
        var colorantes = Coluna(tabelas, OidsImpressora.SuprimentoColoranteIndice);
        var cores = Coluna(tabelas, OidsImpressora.ColoranteValor);

        // A descrição define quais suprimentos existem; sem ela, os índices que têm nível.
        var indices = descricoes.Keys.Union(niveis.Keys).OrderBy(i => i);

        return indices.Select(i =>
        {
            var descricao = descricoes.TryGetValue(i, out var d) ? Limpar(d.Texto) : null;
            string? cor = null;
            if (colorantes.TryGetValue(i, out var c) && c.Numero is > 0
                && cores.TryGetValue(c.Numero.Value, out var valorCor))
            {
                cor = TraduzirCor(Limpar(valorCor.Texto));
            }

            return new SuprimentoImpressoraDto(
                string.IsNullOrEmpty(descricao) ? $"Suprimento {i}" : descricao,
                cor,
                Percentual(niveis.TryGetValue(i, out var n) ? n.Numero : null,
                    maximos.TryGetValue(i, out var m) ? m.Numero : null));
        }).ToList();
    }

    /// <summary>Folhas de uma coluna, indexadas pelo último arco da OID (o índice da linha).</summary>
    private static Dictionary<long, ValorSnmp> Coluna(IReadOnlyList<ValorSnmp> valores, string raiz)
    {
        var prefixo = raiz + ".";
        var resultado = new Dictionary<long, ValorSnmp>();
        foreach (var v in valores)
        {
            if (!v.Oid.StartsWith(prefixo, StringComparison.Ordinal)) continue;
            var resto = v.Oid[prefixo.Length..];
            if (long.TryParse(resto, out var indice))
                resultado[indice] = v;
        }
        return resultado;
    }

    private static string? TraduzirCor(string? cor)
    {
        if (string.IsNullOrEmpty(cor) || cor.Equals("unknown", StringComparison.OrdinalIgnoreCase)) return null;
        return CoresEmPortugues.TryGetValue(cor, out var pt) ? pt : cor;
    }

    // --- Regras do monitoramento ---

    /// <summary>
    /// True quando as <paramref name="falhasParaOffline"/> leituras mais recentes (lista da mais nova
    /// para a mais velha, incluindo a atual) falharam todas. Uma falha isolada não vira alerta.
    /// </summary>
    public static bool DeveAlertarOffline(IReadOnlyList<bool> onlineDasUltimas, int falhasParaOffline)
    {
        var n = Math.Max(1, falhasParaOffline);
        return onlineDasUltimas.Count >= n && onlineDasUltimas.Take(n).All(online => !online);
    }

    /// <summary>Suprimentos com percentual conhecido no limite ou abaixo dele.</summary>
    public static IReadOnlyList<SuprimentoImpressoraDto> SuprimentosBaixos(
        IEnumerable<SuprimentoImpressoraDto> suprimentos, int limitePercentual) =>
        suprimentos.Where(s => s.Percentual is { } p && p <= limitePercentual).ToList();

    /// <summary>Texto do alerta de suprimento: "Toner preto: 8%; Cilindro: 12%".</summary>
    public static string DetalheSuprimentosBaixos(IEnumerable<SuprimentoImpressoraDto> baixos) =>
        Truncar(string.Join("; ", baixos.Select(s => $"{s.Descricao}: {s.Percentual}%")), TamanhoMaximoErro)!;

    /// <summary>
    /// Páginas impressas no período: maior − menor contador válido (não nulo, ≥ 0). Nulo com menos de
    /// duas leituras com contador — uma leitura só não diz quanto foi impresso.
    /// </summary>
    public static long? PaginasNoPeriodo(IEnumerable<long?> contadores)
    {
        var validos = contadores.Where(c => c is >= 0).Select(c => c!.Value).ToList();
        return validos.Count < 2 ? null : validos.Max() - validos.Min();
    }

    // --- JSON dos suprimentos ---

    public static string SerializarSuprimentos(IReadOnlyList<SuprimentoImpressoraDto> suprimentos) =>
        JsonSerializer.Serialize(suprimentos, OpcoesJson);

    /// <summary>Tolerante: JSON vazio ou inválido vira lista vazia (não derruba a tela).</summary>
    public static IReadOnlyList<SuprimentoImpressoraDto> DesserializarSuprimentos(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<SuprimentoImpressoraDto>>(json, OpcoesJson) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static string? Truncar(string? texto, int maximo) =>
        texto is null || texto.Length <= maximo ? texto : texto[..maximo];

    private static string? Limpar(string? texto)
    {
        if (texto is null) return null;
        var limpo = new string(texto.Where(ch => !char.IsControl(ch)).ToArray()).Trim();
        return limpo.Length == 0 ? null : limpo;
    }

    private static string Capitalizar(string texto) =>
        texto.Length == 0 ? texto : char.ToUpperInvariant(texto[0]) + texto[1..];
}
