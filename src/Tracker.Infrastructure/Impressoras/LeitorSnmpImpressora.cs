using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tracker.Application.Impressoras;

namespace Tracker.Infrastructure.Impressoras;

/// <summary>Resultado de uma tentativa: online com os dados, ou offline com o motivo.</summary>
public sealed record ResultadoLeituraSnmp(bool Online, LeituraInterpretada? Dados, string? Erro);

/// <summary>
/// Consulta UMA impressora pela rede (sem banco): GET dos escalares em v2c — e em v1 se não houver
/// resposta —, depois WALK das tabelas de suprimentos na versão que respondeu. Nunca lança por
/// falha de rede: devolve offline com o erro.
/// </summary>
public class LeitorSnmpImpressora
{
    private readonly IClienteSnmp _cliente;
    private readonly IOptionsMonitor<OpcoesSnmp> _opcoes;
    private readonly ILogger<LeitorSnmpImpressora> _logger;

    public LeitorSnmpImpressora(IClienteSnmp cliente, IOptionsMonitor<OpcoesSnmp> opcoes, ILogger<LeitorSnmpImpressora> logger)
    {
        _cliente = cliente;
        _opcoes = opcoes;
        _logger = logger;
    }

    public async Task<ResultadoLeituraSnmp> LerAsync(string endereco, CancellationToken ct)
    {
        var opcoes = _opcoes.CurrentValue;
        var versoes = opcoes.TentarV1 ? new[] { VersaoSnmp.V2c, VersaoSnmp.V1 } : new[] { VersaoSnmp.V2c };

        AlvoSnmp? alvo = null;
        IReadOnlyDictionary<string, ValorSnmp>? escalares = null;
        FalhaSnmpException? ultimaFalha = null;

        foreach (var versao in versoes)
        {
            var tentativa = new AlvoSnmp(endereco, opcoes.Porta, opcoes.Community, opcoes.TimeoutMs, versao);
            try
            {
                escalares = await _cliente.GetAsync(tentativa, OidsImpressora.Escalares, ct);
                alvo = tentativa;
                break;
            }
            catch (FalhaSnmpException ex)
            {
                ultimaFalha = ex;
                if (!ex.SemResposta) break; // DNS/erro de protocolo: outra versão não resolve
            }
        }

        if (alvo is null || escalares is null)
        {
            var motivo = ultimaFalha?.SemResposta == false
                ? ultimaFalha.Message
                : $"Sem resposta SNMP em {opcoes.TimeoutMs} ms (desligada, fora da rede, SNMP desabilitado, " +
                  "community incorreta ou UDP 161 bloqueado).";
            return new ResultadoLeituraSnmp(false, null,
                InterpretacaoSnmpImpressora.Truncar(motivo, InterpretacaoSnmpImpressora.TamanhoMaximoErro));
        }

        // Tabelas de suprimento são opcionais: se uma falhar, a impressora continua online.
        var tabelas = new List<ValorSnmp>();
        foreach (var raiz in OidsImpressora.Tabelas)
        {
            try
            {
                tabelas.AddRange(await _cliente.WalkAsync(alvo, raiz, ct));
            }
            catch (FalhaSnmpException ex)
            {
                _logger.LogDebug(ex, "WALK de {Raiz} falhou em {Endereco}", raiz, endereco);
            }
        }

        return new ResultadoLeituraSnmp(true, InterpretacaoSnmpImpressora.Interpretar(escalares, tabelas), null);
    }
}
