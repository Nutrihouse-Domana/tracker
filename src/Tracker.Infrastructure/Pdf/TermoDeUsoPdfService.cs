using MigraDocCore.DocumentObjectModel;
using MigraDocCore.Rendering;
using PdfSharpCore.Fonts;
using Tracker.Application.Dtos;

namespace Tracker.Infrastructure.Pdf;

public interface ITermoDeUsoPdfService
{
    byte[] Gerar(DadosTermoDeUso dados);
}

/// <summary>
/// Resolve a fonte do termo lendo os arquivos do próprio Windows (nada é versionado no repositório).
/// Preferência: Calibri Light — fiel ao modelo oficial em Word. Fallback: Arial, que está SEMPRE
/// presente no Windows Server. Assim a geração do PDF nunca estoura por falta de fonte em produção,
/// mesmo que a Calibri não esteja instalada no servidor IIS. Ver pegadinha no CLAUDE.md.
/// </summary>
internal sealed class TermoFontResolver : IFontResolver
{
    public static readonly TermoFontResolver Instancia = new();

    public string DefaultFontName => "TermoFont#regular";

    public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        var estilo = (isBold, isItalic) switch
        {
            (true, true) => "bolditalic",
            (true, false) => "bold",
            (false, true) => "italic",
            _ => "regular",
        };
        return new FontResolverInfo($"TermoFont#{estilo}");
    }

    public byte[] GetFont(string faceName)
    {
        var estilo = faceName.Split('#')[^1];
        // Calibri Light (fiel ao modelo) -> Arial (sempre no Windows Server) como fallback por estilo.
        var candidatos = estilo switch
        {
            "bold" => new[] { "calibrib.ttf", "arialbd.ttf" },        // Calibri Light não tem negrito próprio: usa Calibri Bold
            "italic" => new[] { "calibrili.ttf", "ariali.ttf" },
            "bolditalic" => new[] { "calibriz.ttf", "arialbi.ttf" },
            _ => new[] { "calibril.ttf", "arial.ttf" },
        };

        var fontsDir = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        foreach (var arquivo in candidatos)
        {
            var caminho = Path.Combine(fontsDir, arquivo);
            if (File.Exists(caminho)) return File.ReadAllBytes(caminho);
        }
        throw new FileNotFoundException(
            $"Nenhuma fonte disponível para o termo (estilo '{estilo}'). Instale a Calibri ou a Arial no servidor.");
    }
}

/// <summary>
/// Gera o PDF do "Termo de Responsabilidade pela Guarda e Uso de Equipamento de Trabalho" para
/// impressão (MigraDocCore/PdfSharpCore — MIT, sem teto de faturamento). O documento é assinado
/// fisicamente após impresso; a versão escaneada é anexada depois via upload (ver TermosDeUsoController).
/// Layout replica fielmente o modelo oficial em Word ("NOVO MODELO DE TERMO DE RESPONSABILIDADE.doc"):
/// página Carta, Calibri Light 10,5pt, entrelinha simples, espaçamento por linhas em branco.
/// </summary>
public class TermoDeUsoPdfService : ITermoDeUsoPdfService
{
    private const string Fonte = "Calibri Light";
    private const double TamanhoFonte = 10.5;

    // Texto legal fixo da empresa (grupo NUTRIHOUSE) que emite o termo. Transcrito LITERALMENTE do
    // modelo oficial em Word — inclusive o "com endereço com endereço" e as quebras "locadora.6."/
    // "regular.7." sem espaço. Não "corrigir" sem validar com o RH/jurídico (é o texto assinado).
    private const string TextoLegal =
        "Recebi da empresa MCP REFEIÇÕES LTDA, empresa do grupo empresarial NUTRIHOUSE, com endereço " +
        "com endereço na Av. Dr Júlio Maranhão, nº 1210, Guararapes, Jaboatão dos Guararapes-PE, CEP " +
        "54340-000 inscrita no CNPJ nº 06.088.039/0001-99, a título de empréstimo, para meu uso " +
        "exclusivo, conforme determinado na lei, os equipamentos especificados neste termo de " +
        "responsabilidade, comprometendo-me a mantê-los em perfeito estado de conservação, ficando " +
        "ciente de que: 1. O equipamento poderá ser de propriedade da MCP REFEIÇÕES LTDA., de empresa " +
        "do Grupo NUTRIHOUSE ou de empresa terceira/locadora, permanecendo sob minha responsabilidade " +
        "quanto à guarda, conservação e utilização adequada durante todo o período em que estiver sob " +
        "minha posse. 2. Comprometo-me a utilizar o equipamento exclusivamente para suas finalidades " +
        "profissionais, adotando os cuidados necessários à sua conservação e não o cedendo a terceiros " +
        "sem autorização da empresa. 3. Em caso de dano, avaria, perda, extravio, furto, roubo ou falha " +
        "de funcionamento, deverei comunicar imediatamente o setor competente da empresa e prestar as " +
        "informações necessárias à apuração da ocorrência. 4. Havendo comprovação de que o prejuízo " +
        "decorreu de conduta dolosa ou culposa de minha parte, inclusive por negligência, imprudência, " +
        "imperícia ou mau uso, poderei ser responsabilizado pelo prejuízo efetivamente causado, " +
        "inclusive, quando legalmente cabível, mediante desconto em folha de pagamento, nos termos do " +
        "art. 462, §1º, da CLT. Não serão atribuídos ao colaborador danos decorrentes de desgaste " +
        "natural, defeito de fabricação, falha técnica ou utilização regular do equipamento. 5. " +
        "Tratando-se de equipamento locado ou pertencente a terceiro, eventual custo de reparo ou " +
        "reposição somente poderá ser atribuído ao colaborador quando comprovadamente decorrente de " +
        "sua conduta dolosa ou culposa, não sendo automaticamente transferidas ao empregado cobranças, " +
        "multas ou encargos próprios do contrato mantido entre a empresa e a locadora.6. Comprometo-me " +
        "a devolver o equipamento e seus acessórios sempre que solicitado, por ocasião de substituição, " +
        "mudança de função ou desligamento, em condições compatíveis com o estado em que foi recebido, " +
        "ressalvado o desgaste natural decorrente do uso regular.7. Enquanto o equipamento permanecer " +
        "sob minha responsabilidade, poderá ser submetido a inspeções, inventários e verificações " +
        "técnicas pela empresa.";

    // Bloco de devolução: transcrito literalmente do modelo (preenchido à mão quando o bem retorna).
    private const string TextoDevolucao =
        "DEVOLUÇÃO: Atestamos que o bem foi devolvido em (dia)/(mês)/(ano), nas seguintes condições:" +
        "(_) Em perfeito estado (_) Apresentando defeito (_) Faltando peças ou acessórios (assinatura) " +
        "(nome do responsável pelo recebimento do Termo de Responsabilidade).";

    private static readonly object _lockFonte = new();
    private static bool _fontResolverRegistrado;

    public byte[] Gerar(DadosTermoDeUso dados)
    {
        GarantirFontResolver();

        var document = new Document();
        document.Info.Title = $"Termo de Responsabilidade - {dados.NumeroSerie}";

        // Estilo-base = como o modelo Word: Calibri Light 10,5pt, entrelinha simples, sem espaço extra.
        var normal = document.Styles["Normal"];
        normal.Font.Name = Fonte;
        normal.Font.Size = TamanhoFonte;
        normal.ParagraphFormat.LineSpacingRule = LineSpacingRule.Single;
        normal.ParagraphFormat.SpaceBefore = 0;
        normal.ParagraphFormat.SpaceAfter = 0;

        var section = document.AddSection();
        section.PageSetup.PageFormat = PageFormat.Letter;   // 21,59 x 27,94 cm (o modelo é Carta, não A4)
        section.PageSetup.LeftMargin = "3cm";
        section.PageSetup.RightMargin = "3cm";
        section.PageSetup.TopMargin = "2.25cm";
        section.PageSetup.BottomMargin = "0.25cm";

        // Duas linhas em branco no topo (como no modelo).
        LinhaVazia(section);
        LinhaVazia(section);

        var titulo = section.AddParagraph("TERMO DE RESPONSABILIDADE PELA GUARDA E USO DE EQUIPAMENTO DE TRABALHO");
        titulo.Format.Font.Bold = true;
        titulo.Format.Alignment = ParagraphAlignment.Center;

        LinhaVazia(section);

        var subEmpregado = section.AddParagraph("IDENTIFICAÇÃO DO EMPREGADO");
        subEmpregado.Format.Font.Bold = true;
        subEmpregado.Format.Alignment = ParagraphAlignment.Justify;

        LinhaVazia(section);

        AdicionarCampo(section, "Nome:", dados.NomeUsuario);
        AdicionarCampo(section, "CPF:", dados.CpfUsuario);
        AdicionarCampo(section, "Fone para contato:", dados.TelefoneUsuario);
        AdicionarCampo(section, "E-mail:", dados.EmailUsuario);

        LinhaVazia(section);

        var corpo = section.AddParagraph(TextoLegal);
        corpo.Format.Alignment = ParagraphAlignment.Justify;

        LinhaVazia(section);

        // Identificação do equipamento. Tombo = sufixo do hostname (ex.: NHL-36360 -> 36360);
        // S/N = número de série; o estado do dispositivo é informado no sistema ao gerar o termo.
        AdicionarCampo(section, "Estado do dispositivo:", dados.EstadoDispositivo);
        AdicionarCampo(section, "Tombo:", ExtrairTombo(dados.Hostname));
        AdicionarCampo(section, "S/N:", dados.NumeroSerie);

        LinhaVazia(section);

        var dataLinha = section.AddParagraph();
        dataLinha.Format.Font.Bold = true;
        dataLinha.Format.Alignment = ParagraphAlignment.Justify;
        dataLinha.AddText($"Jaboatão dos Guararapes-PE, {dados.DataGeracao:dd/MM/yyyy}.");

        LinhaVazia(section);

        var assinatura = section.AddParagraph();
        assinatura.Format.Alignment = ParagraphAlignment.Justify;
        assinatura.AddText("Assinatura ___________________________________________________ ");

        LinhaVazia(section);

        var devolucao = section.AddParagraph(TextoDevolucao);
        devolucao.Format.Alignment = ParagraphAlignment.Justify;

        var renderer = new PdfDocumentRenderer { Document = document };
        renderer.RenderDocument();

        using var stream = new MemoryStream();
        renderer.PdfDocument.Save(stream);
        return stream.ToArray();
    }

    /// <summary>Registra o resolver de fonte uma única vez (é global no PdfSharpCore).</summary>
    private static void GarantirFontResolver()
    {
        if (_fontResolverRegistrado) return;
        lock (_lockFonte)
        {
            if (_fontResolverRegistrado) return;
            GlobalFontSettings.FontResolver = TermoFontResolver.Instancia;
            _fontResolverRegistrado = true;
        }
    }

    /// <summary>Linha em branco usada como espaçador vertical (como no modelo Word).</summary>
    private static void LinhaVazia(Section section) => section.AddParagraph();

    /// <summary>Rótulo + valor, ambos sem negrito (como no modelo). Vazio fica em branco para preenchimento manual.</summary>
    private static void AdicionarCampo(Section section, string rotulo, string valor)
    {
        var paragrafo = section.AddParagraph();
        paragrafo.Format.Alignment = ParagraphAlignment.Justify;
        paragrafo.AddText(string.IsNullOrWhiteSpace(valor) ? $"{rotulo} " : $"{rotulo} {valor}");
    }

    /// <summary>
    /// Deriva o Tombo do hostname: o número após o prefixo. Ex.: "NHL-36360" -> "36360",
    /// "NHD-1235" -> "1235". Sem hífen, devolve o próprio hostname; sem hostname, vazio.
    /// </summary>
    private static string ExtrairTombo(string? hostname)
    {
        if (string.IsNullOrWhiteSpace(hostname)) return string.Empty;
        var idx = hostname.IndexOf('-');
        return idx >= 0 && idx < hostname.Length - 1 ? hostname[(idx + 1)..].Trim() : hostname.Trim();
    }
}
