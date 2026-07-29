using MigraDocCore.DocumentObjectModel;
using MigraDocCore.Rendering;
using Tracker.Application.Dtos;

namespace Tracker.Infrastructure.Pdf;

public interface ITermoDeUsoPdfService
{
    byte[] Gerar(DadosTermoDeUso dados);
}

/// <summary>
/// Gera o PDF do termo de uso para impressão (MigraDocCore/PdfSharpCore — MIT, sem teto de
/// faturamento, ao contrário do QuestPDF Community). O documento é assinado fisicamente após
/// impresso; a versão escaneada é anexada depois via upload (ver TermosDeUsoController).
/// </summary>
public class TermoDeUsoPdfService : ITermoDeUsoPdfService
{
    public byte[] Gerar(DadosTermoDeUso dados)
    {
        var document = new Document();
        document.Info.Title = $"Termo de Uso - {dados.NumeroSerie}";

        var section = document.AddSection();
        section.PageSetup.LeftMargin = "2.5cm";
        section.PageSetup.RightMargin = "2.5cm";
        section.PageSetup.TopMargin = "2cm";

        var titulo = section.AddParagraph("TERMO DE USO DE EQUIPAMENTO");
        titulo.Format.Font.Size = 16;
        titulo.Format.Font.Bold = true;
        titulo.Format.SpaceAfter = "0.5cm";

        var versao = section.AddParagraph($"Versão do termo: {dados.VersaoTermo}");
        versao.Format.Font.Size = 9;
        versao.Format.Font.Color = Colors.Gray;
        versao.Format.SpaceAfter = "0.8cm";

        AdicionarLinha(section, "Data de geração:", dados.DataGeracao.ToString("dd/MM/yyyy HH:mm"));

        var subUsuario = section.AddParagraph("Dados do responsável");
        subUsuario.Format.Font.Size = 12;
        subUsuario.Format.Font.Bold = true;
        subUsuario.Format.SpaceBefore = "0.5cm";
        subUsuario.Format.SpaceAfter = "0.2cm";

        AdicionarLinha(section, "Nome:", dados.NomeUsuario);
        AdicionarLinha(section, "CPF:", dados.CpfUsuarioMascarado);
        AdicionarLinha(section, "Email:", dados.EmailUsuario);

        var subDispositivo = section.AddParagraph("Dados do equipamento");
        subDispositivo.Format.Font.Size = 12;
        subDispositivo.Format.Font.Bold = true;
        subDispositivo.Format.SpaceBefore = "0.5cm";
        subDispositivo.Format.SpaceAfter = "0.2cm";

        AdicionarLinha(section, "Tipo:", dados.TipoDispositivo);
        AdicionarLinha(section, "Número de série:", dados.NumeroSerie);
        AdicionarLinha(section, "Patrimônio:", dados.AssetTag);
        if (!string.IsNullOrWhiteSpace(dados.Hostname))
            AdicionarLinha(section, "Hostname:", dados.Hostname);

        var textoLegal = section.AddParagraph();
        textoLegal.Format.SpaceBefore = "1cm";
        textoLegal.Format.SpaceAfter = "1.5cm";
        textoLegal.AddText(
            "Declaro ter recebido o equipamento acima descrito, em perfeitas condições de uso, " +
            "comprometendo-me a zelar por sua guarda e conservação, utilizá-lo exclusivamente para " +
            "fins profissionais e devolvê-lo à empresa quando solicitado ou no desligamento, sob pena " +
            "de responder pelos danos ou pelo ressarcimento do valor do equipamento em caso de perda, " +
            "furto ou dano por mau uso.");

        var assinatura = section.AddParagraph();
        assinatura.Format.SpaceBefore = "1.5cm";
        assinatura.AddText("_______________________________________________");
        assinatura.AddLineBreak();
        assinatura.AddText(dados.NomeUsuario);
        assinatura.AddLineBreak();
        assinatura.AddText("Assinatura do responsável");

        var renderer = new PdfDocumentRenderer { Document = document };
        renderer.RenderDocument();

        using var stream = new MemoryStream();
        renderer.PdfDocument.Save(stream);
        return stream.ToArray();
    }

    private static void AdicionarLinha(Section section, string rotulo, string valor)
    {
        var paragrafo = section.AddParagraph();
        var rotuloFormat = paragrafo.AddFormattedText(rotulo, TextFormat.Bold);
        rotuloFormat.AddSpace(1);
        paragrafo.AddText(valor);
    }
}
