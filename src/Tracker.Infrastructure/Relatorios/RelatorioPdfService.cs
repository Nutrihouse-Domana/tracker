using MigraDocCore.DocumentObjectModel;
using MigraDocCore.DocumentObjectModel.Tables;
using MigraDocCore.Rendering;
using Tracker.Application.Relatorios;
using Tracker.Infrastructure.Pdf;

namespace Tracker.Infrastructure.Relatorios;

public interface IRelatorioPdfService
{
    byte[] Inventario(IReadOnlyList<LinhaInventarioDto> linhas, ResumoRelatoriosDto resumo, string geradoPor);
    byte[] Aluguel(IReadOnlyList<LinhaInventarioDto> alugados, ResumoRelatoriosDto resumo, string geradoPor);
    byte[] Unidades(IReadOnlyList<LinhaUnidadeDto> unidades, ResumoRelatoriosDto resumo, string geradoPor);
}

/// <summary>
/// Relatórios do painel em PDF (MigraDocCore/PdfSharpCore, o mesmo motor do termo de uso).
/// Página A4 deitada: as tabelas têm muitas colunas e em retrato o texto quebra demais.
/// A fonte é resolvida por <see cref="PdfFontes"/> — sem isso a geração estoura no servidor.
/// </summary>
public class RelatorioPdfService : IRelatorioPdfService
{
    private const string Fonte = "Calibri Light";
    private static readonly Color Terracota = new(0xB9, 0x57, 0x58);   // cor da marca, igual à do site
    private static readonly Color CinzaLinha = new(0xF2, 0xF2, 0xF2);

    public byte[] Inventario(IReadOnlyList<LinhaInventarioDto> linhas, ResumoRelatoriosDto resumo, string geradoPor)
    {
        var (documento, section) = Abrir("Inventário de ativos", resumo, geradoPor);

        Resumo(section, new[]
        {
            ($"{resumo.TotalAtivos}", "ativos no inventário"),
            ($"{resumo.Alugados}", "alugados"),
            ($"{resumo.Proprios}", "próprios"),
            ($"{resumo.Unidades}", "unidades com ativo"),
        });

        Tabela(section,
            cabecalhos: new[] { "Identificação", "Tipo", "Origem", "Nº de série", "Patrimônio", "Unidade", "Responsável", "Status" },
            larguras: new[] { 4.0, 2.2, 2.0, 3.6, 2.8, 3.6, 4.6, 2.6 },
            linhas: linhas.Select(l => new[]
            {
                l.Identificacao, l.Tipo, l.Origem, l.NumeroSerie,
                l.Patrimonio ?? "—", l.Unidade ?? "—", l.Responsavel ?? "—", l.Status,
            }).ToList());

        return Renderizar(documento);
    }

    public byte[] Aluguel(IReadOnlyList<LinhaInventarioDto> alugados, ResumoRelatoriosDto resumo, string geradoPor)
    {
        var (documento, section) = Abrir("Equipamentos alugados x próprios", resumo, geradoPor);

        Resumo(section, new[]
        {
            ($"{resumo.Alugados}", "alugados"),
            ($"{resumo.Proprios}", "próprios"),
            ($"{Percentual(resumo.Alugados, resumo.TotalAtivos)}", "do parque é alugado"),
            ($"{resumo.TotalAtivos}", "ativos no total"),
        });

        var criterio = section.AddParagraph(
            $"Critério: são considerados alugados os ativos cuja identificação começa com "
            + $"\"{ClassificacaoAluguel.PrefixoAlugado}\". A identificação é o rótulo interno definido pela TI; "
            + "quando ele está vazio, vale o hostname reportado pelo agente e, na falta dele, o número de série.");
        criterio.Format.Font.Size = 8;
        criterio.Format.Font.Italic = true;
        criterio.Format.SpaceAfter = "0.5cm";

        var titulo = section.AddParagraph("Ativos alugados");
        titulo.Format.Font.Size = 12;
        titulo.Format.Font.Bold = true;
        titulo.Format.SpaceAfter = "0.2cm";

        Tabela(section,
            cabecalhos: new[] { "Identificação", "Tipo", "Nº de série", "Patrimônio", "Unidade", "Responsável", "Status", "Vence em" },
            larguras: new[] { 4.2, 2.2, 3.6, 2.8, 3.6, 4.4, 2.6, 2.6 },
            linhas: alugados.Select(l => new[]
            {
                l.Identificacao, l.Tipo, l.NumeroSerie, l.Patrimonio ?? "—",
                l.Unidade ?? "—", l.Responsavel ?? "—", l.Status, TextoVencimento(l.VencimentoAluguel),
            }).ToList());

        return Renderizar(documento);
    }

    public byte[] Unidades(IReadOnlyList<LinhaUnidadeDto> unidades, ResumoRelatoriosDto resumo, string geradoPor)
    {
        var (documento, section) = Abrir("Parque por unidade", resumo, geradoPor);

        Resumo(section, new[]
        {
            ($"{resumo.TotalAtivos}", "ativos no inventário"),
            ($"{resumo.Unidades}", "unidades com ativo"),
            ($"{resumo.Alugados}", "alugados"),
            ($"{resumo.Proprios}", "próprios"),
        });

        Tabela(section,
            cabecalhos: new[] { "Unidade", "Total", "Alugados", "Próprios", "Distribuição por status" },
            larguras: new[] { 6.5, 2.2, 2.4, 2.4, 12.0 },
            linhas: unidades.Select(u => new[]
            {
                u.Unidade,
                u.Total.ToString(),
                u.Alugados.ToString(),
                (u.Total - u.Alugados).ToString(),
                string.Join("   ", u.PorStatus.Select(s => $"{s.Rotulo}: {s.Quantidade}")),
            }).ToList());

        return Renderizar(documento);
    }

    /// <summary>Documento novo já com cabeçalho, título e rodapé com paginação.</summary>
    private static (Document, Section) Abrir(string titulo, ResumoRelatoriosDto resumo, string geradoPor)
    {
        PdfFontes.Garantir();

        var documento = new Document();
        documento.Info.Title = $"Tracker — {titulo}";
        documento.Info.Author = "Tracker Nutrihouse";

        var normal = documento.Styles["Normal"];
        normal.Font.Name = Fonte;
        normal.Font.Size = 9;

        var section = documento.AddSection();
        section.PageSetup.PageFormat = PageFormat.A4;
        section.PageSetup.Orientation = Orientation.Landscape;   // muitas colunas: em retrato o texto quebra demais
        section.PageSetup.LeftMargin = "1.5cm";
        section.PageSetup.RightMargin = "1.5cm";
        section.PageSetup.TopMargin = "1.5cm";
        section.PageSetup.BottomMargin = "1.5cm";

        var cabecalho = section.AddParagraph("TRACKER NUTRIHOUSE");
        cabecalho.Format.Font.Size = 9;
        cabecalho.Format.Font.Bold = true;
        cabecalho.Format.Font.Color = Terracota;

        var linhaTitulo = section.AddParagraph(titulo);
        linhaTitulo.Format.Font.Size = 18;
        linhaTitulo.Format.Font.Bold = true;
        linhaTitulo.Format.SpaceAfter = "0.1cm";

        var subtitulo = section.AddParagraph(
            $"Gerado em {resumo.GeradoEm:dd/MM/yyyy 'às' HH:mm} por {geradoPor}");
        subtitulo.Format.Font.Size = 8.5;
        subtitulo.Format.Font.Color = Colors.Gray;
        subtitulo.Format.SpaceAfter = "0.5cm";

        // Rodapé com "Página X de Y" — relatório impresso costuma circular solto.
        var rodape = section.Footers.Primary.AddParagraph();
        rodape.Format.Font.Size = 8;
        rodape.Format.Font.Color = Colors.Gray;
        rodape.Format.Alignment = ParagraphAlignment.Center;
        rodape.AddText("Página ");
        rodape.AddPageField();
        rodape.AddText(" de ");
        rodape.AddNumPagesField();

        return (documento, section);
    }

    /// <summary>Faixa de números-chave acima da tabela (valor grande + rótulo).</summary>
    private static void Resumo(Section section, IReadOnlyList<(string valor, string rotulo)> cartoes)
    {
        var tabela = section.AddTable();
        tabela.Borders.Width = 0;
        tabela.Rows.LeftIndent = 0;
        foreach (var _ in cartoes) tabela.AddColumn(Unit.FromCentimeter(6.5));

        var linha = tabela.AddRow();
        for (var i = 0; i < cartoes.Count; i++)
        {
            var celula = linha.Cells[i];
            var valor = celula.AddParagraph(cartoes[i].valor);
            valor.Format.Font.Size = 20;
            valor.Format.Font.Bold = true;
            valor.Format.Font.Color = Terracota;
            valor.Format.SpaceAfter = "0.05cm";

            var rotulo = celula.AddParagraph(cartoes[i].rotulo);
            rotulo.Format.Font.Size = 8.5;
            rotulo.Format.Font.Color = Colors.Gray;
        }

        section.AddParagraph().Format.SpaceAfter = "0.4cm";
    }

    /// <summary>Tabela com cabeçalho repetido a cada página e zebrado para leitura em papel.</summary>
    private static void Tabela(Section section, string[] cabecalhos, double[] larguras, IReadOnlyList<string[]> linhas)
    {
        if (linhas.Count == 0)
        {
            var vazio = section.AddParagraph("Nenhum ativo encontrado para este relatório.");
            vazio.Format.Font.Italic = true;
            vazio.Format.Font.Color = Colors.Gray;
            return;
        }

        var tabela = section.AddTable();
        tabela.Borders.Width = 0.25;
        tabela.Borders.Color = new Color(0xDD, 0xDD, 0xDD);
        tabela.LeftPadding = 3;
        tabela.RightPadding = 3;
        tabela.TopPadding = 2;
        tabela.BottomPadding = 2;

        foreach (var largura in larguras) tabela.AddColumn(Unit.FromCentimeter(largura));

        var cabecalho = tabela.AddRow();
        cabecalho.HeadingFormat = true;      // repete o cabeçalho quando a tabela quebra de página
        cabecalho.Shading.Color = Terracota;
        cabecalho.Format.Font.Bold = true;
        cabecalho.Format.Font.Color = Colors.White;
        cabecalho.Format.Font.Size = 8.5;
        for (var i = 0; i < cabecalhos.Length; i++) cabecalho.Cells[i].AddParagraph(cabecalhos[i]);

        for (var i = 0; i < linhas.Count; i++)
        {
            var linha = tabela.AddRow();
            linha.Format.Font.Size = 8.5;
            if (i % 2 == 1) linha.Shading.Color = CinzaLinha;
            for (var c = 0; c < linhas[i].Length; c++) linha.Cells[c].AddParagraph(linhas[i][c]);
        }
    }

    private static byte[] Renderizar(Document documento)
    {
        var renderer = new PdfDocumentRenderer { Document = documento };
        renderer.RenderDocument();

        using var stream = new MemoryStream();
        renderer.PdfDocument.Save(stream);
        return stream.ToArray();
    }

    /// <summary>
    /// Data do vencimento com a situação junto: no papel não há cor nem chip, então "(vencido)" e
    /// "(faltam N dias)" é o que permite bater o olho e achar o contrato que pede ação.
    /// </summary>
    private static string TextoVencimento(DateOnly? vencimento)
    {
        if (vencimento is null) return "—";
        var dias = vencimento.Value.DayNumber - DateOnly.FromDateTime(DateTime.Today).DayNumber;
        var situacao = dias < 0 ? $"vencido há {Math.Abs(dias)}d"
            : dias == 0 ? "vence hoje"
            : dias <= 30 ? $"faltam {dias}d"
            : null;
        return situacao is null ? $"{vencimento:dd/MM/yyyy}" : $"{vencimento:dd/MM/yyyy} ({situacao})";
    }

    private static string Percentual(int parte, int total) =>
        total == 0 ? "0%" : $"{Math.Round(parte * 100.0 / total)}%";
}
