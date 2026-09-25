namespace Tracker.Application.Impressoras;

/// <summary>Versão do protocolo. Só leitura por community (v1/v2c); SNMPv3 não é usado.</summary>
public enum VersaoSnmp
{
    V1,
    V2c
}

/// <summary>Para onde e como consultar: endereço (IP ou DNS), porta UDP, community e timeout por requisição.</summary>
public sealed record AlvoSnmp(string Endereco, int Porta, string Community, int TimeoutMs, VersaoSnmp Versao);

/// <summary>
/// Um valor devolvido pelo agente SNMP, já sem os tipos da biblioteca. Numéricos (Integer32,
/// Counter32/64, Gauge32, TimeTicks) vêm em <paramref name="Numero"/>; OctetString em
/// <paramref name="Texto"/> (decodificado) e <paramref name="Bytes"/> (cru, para campos em bits como
/// hrPrinterDetectedErrorState). noSuchObject/noSuchInstance chegam com tudo nulo.
/// </summary>
public sealed record ValorSnmp(string Oid, long? Numero = null, string? Texto = null, byte[]? Bytes = null);

/// <summary>
/// Rede isolada atrás desta interface: o serviço e o BackgroundService nunca falam UDP direto, e os
/// testes usam um fake. Falhas de comunicação são lançadas como <see cref="FalhaSnmpException"/>.
/// </summary>
public interface IClienteSnmp
{
    /// <summary>GET de várias OIDs escalares. OID inexistente no aparelho volta com valor nulo, não como erro.</summary>
    Task<IReadOnlyDictionary<string, ValorSnmp>> GetAsync(AlvoSnmp alvo, IReadOnlyList<string> oids, CancellationToken ct);

    /// <summary>WALK (GETNEXT) de uma subárvore; devolve as folhas encontradas, em ordem.</summary>
    Task<IReadOnlyList<ValorSnmp>> WalkAsync(AlvoSnmp alvo, string raiz, CancellationToken ct);
}

/// <summary>Falha ao consultar uma impressora. <see cref="SemResposta"/> = timeout/porta fechada (vale tentar outra versão).</summary>
public class FalhaSnmpException : Exception
{
    public FalhaSnmpException(string message, bool semResposta, Exception? inner = null)
        : base(message, inner)
    {
        SemResposta = semResposta;
    }

    public bool SemResposta { get; }
}
