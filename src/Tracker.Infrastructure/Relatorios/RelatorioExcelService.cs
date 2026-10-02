using ClosedXML.Excel;
using Tracker.Application.Relatorios;

namespace Tracker.Infrastructure.Relatorios;

public interface IRelatorioExcelService
{
    byte[] Inventario(IReadOnlyList<LinhaInventarioDto> linhas, ResumoRelatoriosDto resumo);
    byte[] Aluguel(IReadOnlyList<LinhaInventarioDto> alugados, ResumoRelatoriosDto resumo);
    byte[] Unidades(IReadOnlyList<LinhaUnidadeDto> unidades, ResumoRelatoriosDto resumo);
    byte[] Termos(IReadOnlyList<LinhaTermoDto> linhas, ResumoTermosDto resumo);
}

/// <summary>
/// Mesmos relatórios do PDF em planilha (ClosedXML). Diferente do PDF, que é para imprimir, aqui
/// as abas saem com AutoFilter e painel congelado: a TI costuma continuar a análise no Excel.
/// Datas e números vão com o TIPO nativo da célula (não como texto) — senão filtro e ordenação
/// do Excel tratam "10/01/2025" como string e a ordem sai errada.
/// </summary>
public class RelatorioExcelService : IRelatorioExcelService
{
    private static readonly XLColor Terracota = XLColor.FromArgb(0xB9, 0x57, 0x58);

    public byte[] Inventario(IReadOnlyList<LinhaInventarioDto> linhas, ResumoRelatoriosDto resumo)
    {
        using var livro = new XLWorkbook();

        var aba = livro.Worksheets.Add("Inventário");
        var cabecalhos = new[] { "Identificação", "Tipo", "Origem", "Nº de série", "Patrimônio", "Unidade", "Responsável", "Status", "Cadastrado em" };
        EscreverCabecalho(aba, cabecalhos);

        var linha = 2;
        foreach (var l in linhas)
        {
            aba.Cell(linha, 1).Value = l.Identificacao;
            aba.Cell(linha, 2).Value = l.Tipo;
            aba.Cell(linha, 3).Value = l.Origem;
            aba.Cell(linha, 4).Value = l.NumeroSerie;
            aba.Cell(linha, 5).Value = l.Patrimonio ?? string.Empty;
            aba.Cell(linha, 6).Value = l.Unidade ?? string.Empty;
            aba.Cell(linha, 7).Value = l.Responsavel ?? string.Empty;
            aba.Cell(linha, 8).Value = l.Status;
            aba.Cell(linha, 9).Value = l.CadastradoEm;
            aba.Cell(linha, 9).Style.DateFormat.Format = "dd/mm/yyyy";
            linha++;
        }

        Finalizar(aba, cabecalhos.Length, linha - 1);
        AbaResumo(livro, resumo);
        return Salvar(livro);
    }

    public byte[] Aluguel(IReadOnlyList<LinhaInventarioDto> alugados, ResumoRelatoriosDto resumo)
    {
        using var livro = new XLWorkbook();

        var aba = livro.Worksheets.Add("Alugados");
        var cabecalhos = new[] { "Identificação", "Tipo", "Nº de série", "Patrimônio", "Unidade", "Responsável", "Status", "Cadastrado em", "Vence em", "Dias restantes" };
        EscreverCabecalho(aba, cabecalhos);

        var linha = 2;
        foreach (var l in alugados)
        {
            aba.Cell(linha, 1).Value = l.Identificacao;
            aba.Cell(linha, 2).Value = l.Tipo;
            aba.Cell(linha, 3).Value = l.NumeroSerie;
            aba.Cell(linha, 4).Value = l.Patrimonio ?? string.Empty;
            aba.Cell(linha, 5).Value = l.Unidade ?? string.Empty;
            aba.Cell(linha, 6).Value = l.Responsavel ?? string.Empty;
            aba.Cell(linha, 7).Value = l.Status;
            aba.Cell(linha, 8).Value = l.CadastradoEm;
            aba.Cell(linha, 8).Style.DateFormat.Format = "dd/mm/yyyy";
            if (l.VencimentoAluguel is { } vence)
            {
                // ClosedXML não escreve DateOnly: vira DateTime à meia-noite só na hora de gravar.
                aba.Cell(linha, 9).Value = vence.ToDateTime(TimeOnly.MinValue);
                aba.Cell(linha, 9).Style.DateFormat.Format = "dd/mm/yyyy";
                // Número (não texto) para a TI ordenar e filtrar "o que vence em menos de X dias".
                aba.Cell(linha, 10).Value = vence.DayNumber - DateOnly.FromDateTime(DateTime.Today).DayNumber;
            }
            linha++;
        }

        Finalizar(aba, cabecalhos.Length, linha - 1);

        var comparativo = livro.Worksheets.Add("Alugados x próprios");
        EscreverCabecalho(comparativo, new[] { "Origem", "Quantidade", "% do parque" });
        comparativo.Cell(2, 1).Value = "Alugado";
        comparativo.Cell(2, 2).Value = resumo.Alugados;
        comparativo.Cell(2, 3).Value = Fracao(resumo.Alugados, resumo.TotalAtivos);
        comparativo.Cell(3, 1).Value = "Próprio";
        comparativo.Cell(3, 2).Value = resumo.Proprios;
        comparativo.Cell(3, 3).Value = Fracao(resumo.Proprios, resumo.TotalAtivos);
        comparativo.Range(2, 3, 3, 3).Style.NumberFormat.Format = "0%";
        Finalizar(comparativo, 3, 3);

        var criterio = comparativo.Cell(5, 1);
        criterio.Value = $"Critério: alugado = identificação começa com \"{ClassificacaoAluguel.PrefixoAlugado}\" (impressoras: interruptor \"Alugada\" do cadastro).";
        criterio.Style.Font.Italic = true;
        criterio.Style.Font.FontColor = XLColor.Gray;

        AbaResumo(livro, resumo);
        return Salvar(livro);
    }

    public byte[] Unidades(IReadOnlyList<LinhaUnidadeDto> unidades, ResumoRelatoriosDto resumo)
    {
        using var livro = new XLWorkbook();

        var aba = livro.Worksheets.Add("Por unidade");
        var cabecalhos = new[] { "Unidade", "Total", "Alugados", "Próprios", "Distribuição por status" };
        EscreverCabecalho(aba, cabecalhos);

        var linha = 2;
        foreach (var u in unidades)
        {
            aba.Cell(linha, 1).Value = u.Unidade;
            aba.Cell(linha, 2).Value = u.Total;
            aba.Cell(linha, 3).Value = u.Alugados;
            aba.Cell(linha, 4).Value = u.Total - u.Alugados;
            aba.Cell(linha, 5).Value = string.Join(" | ", u.PorStatus.Select(s => $"{s.Rotulo}: {s.Quantidade}"));
            linha++;
        }

        Finalizar(aba, cabecalhos.Length, linha - 1);
        AbaResumo(livro, resumo);
        return Salvar(livro);
    }

    public byte[] Termos(IReadOnlyList<LinhaTermoDto> linhas, ResumoTermosDto resumo)
    {
        using var livro = new XLWorkbook();

        var aba = livro.Worksheets.Add("Termos");
        var cabecalhos = new[] { "Identificação", "Tipo", "Nº de série", "Patrimônio", "Unidade", "Detentor", "Status", "Termo", "Versão", "Data do termo", "Termo anterior de" };
        EscreverCabecalho(aba, cabecalhos);

        var linha = 2;
        foreach (var l in linhas)
        {
            aba.Cell(linha, 1).Value = l.Identificacao;
            aba.Cell(linha, 2).Value = l.Tipo;
            aba.Cell(linha, 3).Value = l.NumeroSerie;
            aba.Cell(linha, 4).Value = l.Patrimonio ?? string.Empty;
            aba.Cell(linha, 5).Value = l.Unidade ?? string.Empty;
            aba.Cell(linha, 6).Value = l.Detentor ?? string.Empty;
            aba.Cell(linha, 7).Value = l.Status;
            aba.Cell(linha, 8).Value = l.Situacao.Rotulo();
            if (l.Situacao == SituacaoTermo.SemTermo) aba.Cell(linha, 8).Style.Font.FontColor = XLColor.Red;
            aba.Cell(linha, 9).Value = l.VersaoTermo ?? string.Empty;
            if (l.DataAceite is { } data)
            {
                aba.Cell(linha, 10).Value = data;
                aba.Cell(linha, 10).Style.DateFormat.Format = "dd/mm/yyyy";
            }
            aba.Cell(linha, 11).Value = l.TermoAnteriorDe ?? string.Empty;
            linha++;
        }

        Finalizar(aba, cabecalhos.Length, linha - 1);

        var totais = livro.Worksheets.Add("Resumo");
        EscreverCabecalho(totais, new[] { "Indicador", "Valor" });
        var dados = new (string, int)[]
        {
            ("Equipamentos", resumo.Equipamentos),
            ("Com termo do detentor atual", resumo.ComTermo),
            ("Com detentor e sem termo", resumo.SemTermo),
            ("Sem detentor (não se aplica)", resumo.SemDetentor),
        };
        for (var i = 0; i < dados.Length; i++)
        {
            totais.Cell(i + 2, 1).Value = dados[i].Item1;
            totais.Cell(i + 2, 2).Value = dados[i].Item2;
        }
        totais.Cell(dados.Length + 3, 1).Value = "Gerado em";
        totais.Cell(dados.Length + 3, 2).Value = resumo.GeradoEm;
        totais.Cell(dados.Length + 3, 2).Style.DateFormat.Format = "dd/mm/yyyy hh:mm";
        totais.Columns(1, 2).AdjustToContents();

        return Salvar(livro);
    }

    /// <summary>Aba fixa com os totais do parque — o mesmo bloco de números do topo do PDF.</summary>
    private static void AbaResumo(XLWorkbook livro, ResumoRelatoriosDto resumo)
    {
        var aba = livro.Worksheets.Add("Resumo");
        EscreverCabecalho(aba, new[] { "Indicador", "Valor" });

        var dados = new List<(string, object)>
        {
            ("Ativos no inventário", resumo.TotalAtivos),
            ("Alugados", resumo.Alugados),
            ("Próprios", resumo.Proprios),
            ("Unidades com ativo", resumo.Unidades),
        };
        dados.AddRange(resumo.PorTipo.Select(t => ($"Tipo — {t.Rotulo}", (object)t.Quantidade)));
        dados.AddRange(resumo.PorStatus.Select(s => ($"Status — {s.Rotulo}", (object)s.Quantidade)));

        var linha = 2;
        foreach (var (rotulo, valor) in dados)
        {
            aba.Cell(linha, 1).Value = rotulo;
            aba.Cell(linha, 2).Value = Convert.ToInt32(valor);
            linha++;
        }

        aba.Cell(linha + 1, 1).Value = "Gerado em";
        aba.Cell(linha + 1, 2).Value = resumo.GeradoEm;
        aba.Cell(linha + 1, 2).Style.DateFormat.Format = "dd/mm/yyyy hh:mm";

        aba.Columns(1, 2).AdjustToContents();
    }

    private static void EscreverCabecalho(IXLWorksheet aba, IReadOnlyList<string> cabecalhos)
    {
        for (var i = 0; i < cabecalhos.Count; i++)
        {
            var celula = aba.Cell(1, i + 1);
            celula.Value = cabecalhos[i];
            celula.Style.Fill.BackgroundColor = Terracota;
            celula.Style.Font.FontColor = XLColor.White;
            celula.Style.Font.Bold = true;
        }
    }

    /// <summary>Congela o cabeçalho, liga o filtro e ajusta a largura das colunas.</summary>
    private static void Finalizar(IXLWorksheet aba, int colunas, int ultimaLinha)
    {
        aba.SheetView.FreezeRows(1);
        if (ultimaLinha >= 1) aba.Range(1, 1, Math.Max(ultimaLinha, 1), colunas).SetAutoFilter();
        aba.Columns(1, colunas).AdjustToContents();
        // Coluna larga demais (observação, lista de status) empurra as outras para fora da tela.
        foreach (var coluna in aba.Columns(1, colunas))
            if (coluna.Width > 55) coluna.Width = 55;
    }

    private static byte[] Salvar(XLWorkbook livro)
    {
        using var stream = new MemoryStream();
        livro.SaveAs(stream);
        return stream.ToArray();
    }

    private static double Fracao(int parte, int total) => total == 0 ? 0 : (double)parte / total;
}
