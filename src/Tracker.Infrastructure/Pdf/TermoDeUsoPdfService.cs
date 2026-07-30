using MigraDocCore.DocumentObjectModel;
using MigraDocCore.Rendering;
using Tracker.Application.Dtos;

namespace Tracker.Infrastructure.Pdf;

public interface ITermoDeUsoPdfService
{
    byte[] Gerar(DadosTermoDeUso dados);
}

/// <summary>
/// Gera o PDF do "Termo de Responsabilidade pela Guarda e Uso de Equipamento de Trabalho" para
/// impressão (MigraDocCore/PdfSharpCore — MIT, sem teto de faturamento). O documento é assinado
/// fisicamente após impresso; a versão escaneada é anexada depois via upload (ver TermosDeUsoController).
/// </summary>
public class TermoDeUsoPdfService : ITermoDeUsoPdfService
{
    // Texto legal fixo da empresa (grupo NUTRIHOUSE) que emite o termo.
    private const string TextoLegal =
        "Recebi da empresa MCP REFEIÇÕES LTDA, empresa do grupo empresarial NUTRIHOUSE, com endereço na " +
        "Av. Dr Júlio Maranhão, nº 1210, Guararapes, Jaboatão dos Guararapes-PE, CEP 54340-000, inscrita no " +
        "CNPJ nº 06.088.039/0001-99, a título de empréstimo, para meu uso exclusivo, conforme determinado na " +
        "lei, os equipamentos especificados neste termo de responsabilidade, comprometendo-me a mantê-los em " +
        "perfeito estado de conservação, ficando ciente de que: " +
        "1- Se o equipamento for danificado ou inutilizado por emprego inadequado, mau uso, negligência ou " +
        "extravio, a empresa me fornecerá novo equipamento e cobrará o valor para aquisição de um equipamento " +
        "da mesma marca ou equivalente ao da praça, com descontos na folha de pagamento, independente de culpa " +
        "ou dolo. " +
        "2- Em caso de dano, inutilização ou extravio do equipamento deverei comunicar, imediatamente ao setor " +
        "competente da empresa. " +
        "3- Terminando os serviços ou no caso de rescisão do contrato de trabalho, devolverei o equipamento " +
        "completo e em perfeito estado de conservação, considerando-se o tempo do uso dele, ao setor competente. " +
        "4- Estando os equipamentos em minha posse, estarei sujeito a inspeções sem prévio aviso.";

    public byte[] Gerar(DadosTermoDeUso dados)
    {
        var document = new Document();
        document.Info.Title = $"Termo de Responsabilidade - {dados.NumeroSerie}";

        var section = document.AddSection();
        section.PageSetup.LeftMargin = "2.5cm";
        section.PageSetup.RightMargin = "2.5cm";
        section.PageSetup.TopMargin = "2cm";
        section.PageSetup.BottomMargin = "2cm";

        var titulo = section.AddParagraph("TERMO DE RESPONSABILIDADE PELA GUARDA E USO DE EQUIPAMENTO DE TRABALHO");
        titulo.Format.Font.Size = 13;
        titulo.Format.Font.Bold = true;
        titulo.Format.Alignment = ParagraphAlignment.Center;
        titulo.Format.SpaceAfter = "0.7cm";

        var subEmpregado = section.AddParagraph("IDENTIFICAÇÃO DO EMPREGADO");
        subEmpregado.Format.Font.Size = 11;
        subEmpregado.Format.Font.Bold = true;
        subEmpregado.Format.SpaceAfter = "0.3cm";

        AdicionarCampo(section, "Nome:", dados.NomeUsuario);
        AdicionarCampo(section, "CPF:", dados.CpfUsuario);
        AdicionarCampo(section, "Fone para contato:", dados.TelefoneUsuario);
        AdicionarCampo(section, "E-mail:", dados.EmailUsuario);

        var corpo = section.AddParagraph(TextoLegal);
        corpo.Format.Alignment = ParagraphAlignment.Justify;
        corpo.Format.SpaceBefore = "0.6cm";
        corpo.Format.SpaceAfter = "0.7cm";
        corpo.Format.LineSpacingRule = LineSpacingRule.OnePtFive;

        // Identificação do equipamento: "Estado do dispositivo" é preenchido à mão na entrega.
        AdicionarCampo(section, "Estado do dispositivo:", string.Empty);
        AdicionarCampo(section, "Tombo:", dados.AssetTag);
        AdicionarCampo(section, "S/N:", dados.NumeroSerie);

        var assinatura = section.AddParagraph();
        assinatura.Format.SpaceBefore = "1.5cm";
        assinatura.AddText("Assinatura ______________________________________________________");

        // Bloco de devolução: preenchido à mão no momento em que o bem retorna.
        var devolucao = section.AddParagraph();
        devolucao.Format.SpaceBefore = "1.5cm";
        devolucao.Format.Alignment = ParagraphAlignment.Justify;
        devolucao.Format.LineSpacingRule = LineSpacingRule.OnePtFive;
        devolucao.AddFormattedText("DEVOLUÇÃO: ", TextFormat.Bold);
        devolucao.AddText(
            "Atestamos que o bem foi devolvido em ____/____/______, nas seguintes condições: " +
            "(  ) Em perfeito estado  (  ) Apresentando defeito  (  ) Faltando peças ou acessórios.");

        var respRecebimento = section.AddParagraph();
        respRecebimento.Format.SpaceBefore = "1.2cm";
        respRecebimento.AddText("________________________________________________________________");
        respRecebimento.AddLineBreak();
        respRecebimento.AddText("Assinatura e nome do responsável pelo recebimento do Termo de Responsabilidade.");

        // Rodapé discreto para rastreabilidade (versão do termo + data de geração).
        var rodape = section.AddParagraph($"Versão do termo: {dados.VersaoTermo} — Gerado em {dados.DataGeracao:dd/MM/yyyy HH:mm}");
        rodape.Format.Font.Size = 8;
        rodape.Format.Font.Color = Colors.Gray;
        rodape.Format.SpaceBefore = "1.5cm";

        var renderer = new PdfDocumentRenderer { Document = document };
        renderer.RenderDocument();

        using var stream = new MemoryStream();
        renderer.PdfDocument.Save(stream);
        return stream.ToArray();
    }

    /// <summary>Rótulo em negrito + valor; se o valor estiver vazio, imprime uma linha para preenchimento manual.</summary>
    private static void AdicionarCampo(Section section, string rotulo, string valor)
    {
        var paragrafo = section.AddParagraph();
        paragrafo.Format.SpaceAfter = "0.15cm";
        paragrafo.AddFormattedText(rotulo, TextFormat.Bold);
        paragrafo.AddSpace(1);
        paragrafo.AddText(string.IsNullOrWhiteSpace(valor)
            ? "______________________________________________"
            : valor);
    }
}
