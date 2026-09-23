using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tracker.Api.Seguranca;
using Tracker.Application.Relatorios;
using Tracker.Application.Seguranca;
using Tracker.Infrastructure.Relatorios;

namespace Tracker.Api.Controllers;

/// <summary>
/// Relatórios do inventário, baixados pelo menu da conta: PDF (imprimir) e XLSX (continuar a
/// análise no Excel). Tudo é LEITURA do parque ativo, então basta
/// <see cref="Permissoes.DispositivosVer"/> e não há registro de auditoria (que é para escrita).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = Permissoes.DispositivosVer)]
public class RelatoriosController : ControllerBase
{
    private const string TipoConteudoXlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly IRelatorioInventarioService _dados;
    private readonly IRelatorioPdfService _pdf;
    private readonly IRelatorioExcelService _excel;

    public RelatoriosController(
        IRelatorioInventarioService dados,
        IRelatorioPdfService pdf,
        IRelatorioExcelService excel)
    {
        _dados = dados;
        _pdf = pdf;
        _excel = excel;
    }

    /// <summary>Baixa um relatório. <paramref name="formato"/> aceita <c>pdf</c> ou <c>xlsx</c>.</summary>
    [HttpGet("{tipo}/{formato}")]
    public async Task<IActionResult> Baixar(TipoRelatorio tipo, string formato, CancellationToken ct)
    {
        var emPdf = formato.Equals("pdf", StringComparison.OrdinalIgnoreCase);
        if (!emPdf && !formato.Equals("xlsx", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { erro = "Formato inválido. Use 'pdf' ou 'xlsx'." });

        var resumo = await _dados.ResumoAsync(ct);
        var geradoPor = User.AdmNome();

        var (conteudo, nome) = tipo switch
        {
            TipoRelatorio.Inventario when emPdf =>
                (_pdf.Inventario(await _dados.InventarioAsync(false, ct), resumo, geradoPor), "inventario"),
            TipoRelatorio.Inventario =>
                (_excel.Inventario(await _dados.InventarioAsync(false, ct), resumo), "inventario"),

            TipoRelatorio.Aluguel when emPdf =>
                (_pdf.Aluguel(await _dados.InventarioAsync(true, ct), resumo, geradoPor), "alugados-x-proprios"),
            TipoRelatorio.Aluguel =>
                (_excel.Aluguel(await _dados.InventarioAsync(true, ct), resumo), "alugados-x-proprios"),

            TipoRelatorio.Unidades when emPdf =>
                (_pdf.Unidades(await _dados.PorUnidadeAsync(ct), resumo, geradoPor), "parque-por-unidade"),
            _ =>
                (_excel.Unidades(await _dados.PorUnidadeAsync(ct), resumo), "parque-por-unidade"),
        };

        // A data no nome evita que dois downloads do mesmo relatório se sobreponham na pasta.
        var arquivo = $"tracker-{nome}-{resumo.GeradoEm:yyyy-MM-dd-HHmm}.{(emPdf ? "pdf" : "xlsx")}";
        return File(conteudo, emPdf ? "application/pdf" : TipoConteudoXlsx, arquivo);
    }
}
