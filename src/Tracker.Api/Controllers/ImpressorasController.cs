using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tracker.Api.Seguranca;
using Tracker.Application.Dtos;
using Tracker.Application.Seguranca;
using Tracker.Infrastructure.Impressoras;

namespace Tracker.Api.Controllers;

/// <summary>
/// Monitoramento SNMP das impressoras (o cadastro fica em DispositivosController). 404 para id
/// inexistente/arquivado ou que não é impressora; 400 quando a impressora não é monitorável.
/// </summary>
[ApiController]
[Route("api/impressoras")]
[Authorize]
public class ImpressorasController : ControllerBase
{
    private readonly MonitoramentoImpressorasService _monitoramento;
    private readonly ILogger<ImpressorasController> _logger;

    public ImpressorasController(MonitoramentoImpressorasService monitoramento, ILogger<ImpressorasController> logger)
    {
        _monitoramento = monitoramento;
        _logger = logger;
    }

    /// <summary>
    /// Último estado lido. Não recusa impressora não monitorável: o último estado (e o campo
    /// <c>monitoravel</c>) continuam úteis depois que o monitoramento é desligado.
    /// </summary>
    [HttpGet("{id:guid}/monitoramento")]
    [Authorize(Policy = Permissoes.DispositivosVer)]
    public async Task<ActionResult<MonitoramentoImpressoraDto>> Monitoramento(Guid id, CancellationToken ct) =>
        Responder(await _monitoramento.ObterMonitoramentoAsync(id, ct));

    /// <summary>
    /// Leituras do período e páginas impressas nele. <c>de</c>/<c>ate</c> são dias civis de Brasília
    /// (inclusivos; padrão = mês corrente até hoje), convertidos por <c>FusoBrasil</c>.
    /// </summary>
    [HttpGet("{id:guid}/leituras")]
    [Authorize(Policy = Permissoes.DispositivosVer)]
    public async Task<ActionResult<LeiturasImpressoraDto>> Leituras(
        Guid id, [FromQuery] DateOnly? de, [FromQuery] DateOnly? ate, CancellationToken ct) =>
        Responder(await _monitoramento.ObterLeiturasAsync(id, de, ate, ct));

    /// <summary>
    /// Lê a impressora agora e devolve o estado. SEM auditoria de propósito: não muda cadastro,
    /// responsável nem status do ativo — só grava telemetria (a mesma que o ciclo grava a cada 15
    /// min), e a própria leitura fica no histórico. Quem pediu vai para o log da aplicação.
    /// </summary>
    [HttpPost("{id:guid}/ler-agora")]
    [Authorize(Policy = Permissoes.DispositivosCadastrar)]
    public async Task<ActionResult<MonitoramentoImpressoraDto>> LerAgora(Guid id, CancellationToken ct)
    {
        _logger.LogInformation("Leitura SNMP manual da impressora {Id} pedida por {Adm}.", id, User.AdmNome());
        return Responder(await _monitoramento.LerERegistrarAsync(id, ct));
    }

    private ActionResult<T> Responder<T>(ResultadoConsultaImpressora<T> resultado) => resultado.Situacao switch
    {
        SituacaoConsultaImpressora.Ok => Ok(resultado.Valor),
        SituacaoConsultaImpressora.NaoEncontrada => NotFound(new { message = resultado.Mensagem }),
        _ => BadRequest(new { message = resultado.Mensagem }),
    };
}
