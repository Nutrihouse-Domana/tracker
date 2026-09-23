using System.Text;
using Tracker.Application.Relatorios;
using Tracker.Infrastructure.Relatorios;
using Xunit;

namespace Tracker.Api.Tests;

/// <summary>
/// Protege a regra de origem do ativo (alugado x próprio) e a geração dos arquivos de relatório.
/// A regra tem um espelho no site (features/dispositivos/aluguel.ts): se um lado mudar sozinho,
/// a coluna da tela e o relatório passam a discordar sobre o mesmo aparelho.
/// </summary>
public class RelatoriosTests
{
    private static DateOnly Hoje => DateOnly.FromDateTime(DateTime.Today);

    private static ResumoRelatoriosDto ResumoFake(int total = 3, int alugados = 1) =>
        new(
            TotalAtivos: total,
            Alugados: alugados,
            Proprios: total - alugados,
            Unidades: 2,
            PorTipo: new[] { new ContagemDto("Notebook", total) },
            PorStatus: new[] { new ContagemDto("Em estoque", total) },
            GeradoEm: new DateTime(2026, 9, 23, 14, 30, 0));

    private static LinhaInventarioDto Linha(string identificacao, string origem = "Próprio", DateOnly? vencimento = null) =>
        new(identificacao, "Notebook", origem, "SN-1", "PAT-1", "Matriz", "Fulano", "Em estoque",
            new DateTime(2025, 1, 10), vencimento);

    [Theory]
    [InlineData("MAQ-0142", true)]
    [InlineData("maq-0142", true)]      // a TI digita em caixa baixa; continua alugado
    [InlineData("  MAQ-0142", true)]    // espaço à esquerda não muda a origem do ativo
    [InlineData("NHD-0031", false)]
    [InlineData("NHL-0075", false)]
    [InlineData("MAQUINA-DO-JOAO", true)] // documenta o efeito colateral do prefixo: começa com MAQ
    [InlineData("", false)]
    [InlineData(null, false)]
    public void EhAlugado_SegueOPrefixoDaIdentificacao(string? identificacao, bool esperado)
    {
        Assert.Equal(esperado, ClassificacaoAluguel.EhAlugado(identificacao));
    }

    /// <summary>
    /// Mesma precedência das telas. O hostname entra só quando a TI não definiu rótulo — e como o
    /// heartbeat o sobrescreve, um ativo sem rótulo pode trocar de classificação sozinho.
    /// </summary>
    [Theory]
    [InlineData("MAQ-01", "NHD-HOST", "SN-9", "MAQ-01")]
    [InlineData(null, "NHD-HOST", "SN-9", "NHD-HOST")]
    [InlineData(null, null, "SN-9", "SN-9")]
    [InlineData("   ", null, "SN-9", "SN-9")]
    public void IdentificacaoExibida_PrefereRotuloDaTiDepoisHostnameDepoisSerie(
        string? interna, string? hostname, string serie, string esperado)
    {
        Assert.Equal(esperado, ClassificacaoAluguel.IdentificacaoExibida(interna, hostname, serie));
    }

    [Fact]
    public void RelatorioPdf_GeraArquivoPdfValido()
    {
        var pdf = new RelatorioPdfService();

        var bytes = pdf.Inventario(
            new[] { Linha("MAQ-0142", "Alugado"), Linha("NHD-0031") },
            ResumoFake(),
            "Tester");

        Assert.NotEmpty(bytes);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
    }

    /// <summary>Relatório sem nenhuma linha ainda precisa sair (com o aviso de vazio), não estourar.</summary>
    [Fact]
    public void RelatorioPdf_SemLinhas_NaoQuebra()
    {
        var pdf = new RelatorioPdfService();

        var bytes = pdf.Aluguel(Array.Empty<LinhaInventarioDto>(), ResumoFake(total: 0, alugados: 0), "Tester");

        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
    }

    [Fact]
    public void RelatorioExcel_GeraArquivoXlsxValido()
    {
        var excel = new RelatorioExcelService();

        var bytes = excel.Inventario(new[] { Linha("MAQ-0142", "Alugado") }, ResumoFake());

        Assert.NotEmpty(bytes);
        // XLSX é um zip: começa com a assinatura "PK\x03\x04".
        Assert.Equal(new byte[] { 0x50, 0x4B, 0x03, 0x04 }, bytes.Take(4).ToArray());
    }

    /// <summary>
    /// O relatório de alugados é o que a TI usa para renovar contrato: precisa sair com o
    /// vencimento mesmo quando parte dos ativos não tem data registrada.
    /// </summary>
    [Fact]
    public void RelatorioAluguel_ComEsemVencimento_GeraOsDoisFormatos()
    {
        var alugados = new[]
        {
            Linha("MAQ-0142", "Alugado", Hoje.AddDays(10)),   // dentro da janela de alerta
            Linha("MAQ-0187", "Alugado", Hoje.AddDays(-5)),   // vencido
            Linha("MAQ-0200", "Alugado"),                               // sem contrato registrado
        };
        var resumo = ResumoFake(total: 3, alugados: 3);

        var pdf = new RelatorioPdfService().Aluguel(alugados, resumo, "Tester");
        var excel = new RelatorioExcelService().Aluguel(alugados, resumo);

        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
        Assert.Equal(new byte[] { 0x50, 0x4B, 0x03, 0x04 }, excel.Take(4).ToArray());
    }

    [Fact]
    public void RelatorioExcel_PorUnidade_GeraArquivo()
    {
        var excel = new RelatorioExcelService();

        var bytes = excel.Unidades(
            new[] { new LinhaUnidadeDto("Matriz", 5, 2, new[] { new ContagemDto("Em uso", 5) }) },
            ResumoFake(total: 5, alugados: 2));

        Assert.Equal(new byte[] { 0x50, 0x4B, 0x03, 0x04 }, bytes.Take(4).ToArray());
    }
}
