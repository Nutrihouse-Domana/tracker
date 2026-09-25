using System.Net;
using System.Net.Sockets;
using Lextm.SharpSnmpLib;
using Lextm.SharpSnmpLib.Messaging;
using Tracker.Application.Impressoras;

namespace Tracker.Infrastructure.Impressoras;

/// <summary>
/// <see cref="IClienteSnmp"/> sobre a Lextm.SharpSnmpLib. Usa os métodos SÍNCRONOS da biblioteca
/// (os únicos com timeout) dentro de <see cref="Task.Run(Action)"/>: os assíncronos dela não têm
/// timeout nem cancelamento e esperariam para sempre por uma impressora desligada.
/// </summary>
public class ClienteSnmpSharpSnmp : IClienteSnmp
{
    public async Task<IReadOnlyDictionary<string, ValorSnmp>> GetAsync(
        AlvoSnmp alvo, IReadOnlyList<string> oids, CancellationToken ct)
    {
        var endpoint = await ResolverAsync(alvo, ct);
        return await Task.Run(() => Executar(() => GetSincrono(alvo, endpoint, oids)), ct);
    }

    public async Task<IReadOnlyList<ValorSnmp>> WalkAsync(AlvoSnmp alvo, string raiz, CancellationToken ct)
    {
        var endpoint = await ResolverAsync(alvo, ct);
        return await Task.Run(() => Executar(() =>
        {
            var lista = new List<Variable>();
            try
            {
                Messenger.Walk(Versao(alvo), endpoint, new OctetString(alvo.Community),
                    new ObjectIdentifier(raiz), lista, alvo.TimeoutMs, WalkMode.WithinSubtree);
            }
            catch (ErrorException)
            {
                // v1 sinaliza o fim da MIB (ou coluna inexistente) com noSuchName: vale o que já veio.
            }
            return (IReadOnlyList<ValorSnmp>)lista.Select(Converter).ToList();
        }), ct);
    }

    private static IReadOnlyDictionary<string, ValorSnmp> GetSincrono(
        AlvoSnmp alvo, IPEndPoint endpoint, IReadOnlyList<string> oids)
    {
        var community = new OctetString(alvo.Community);
        IList<Variable> Pedir(IEnumerable<string> lista) =>
            Messenger.Get(Versao(alvo), endpoint, community,
                lista.Select(o => new Variable(new ObjectIdentifier(o))).ToList(), alvo.TimeoutMs);

        var resultado = new Dictionary<string, ValorSnmp>();
        try
        {
            foreach (var v in Pedir(oids).Select(Converter))
                resultado[v.Oid] = v;
        }
        catch (ErrorException)
        {
            // Em v1 (e em alguns agentes v2c) UMA OID inexistente derruba o GET inteiro com
            // noSuchName. Pede uma a uma e deixa nula a que não existe.
            foreach (var oid in oids)
            {
                try
                {
                    foreach (var v in Pedir([oid]).Select(Converter))
                        resultado[v.Oid] = v;
                }
                catch (ErrorException)
                {
                    resultado[oid] = new ValorSnmp(oid);
                }
            }
        }
        return resultado;
    }

    /// <summary>Traduz as exceções da biblioteca/rede para <see cref="FalhaSnmpException"/>.</summary>
    private static T Executar<T>(Func<T> acao)
    {
        try
        {
            return acao();
        }
        catch (Lextm.SharpSnmpLib.Messaging.TimeoutException ex)
        {
            throw new FalhaSnmpException("Sem resposta SNMP no tempo limite.", semResposta: true, ex);
        }
        catch (SocketException ex)
        {
            // No Windows, "porta UDP inalcançável" (ICMP) chega como ConnectionReset.
            throw new FalhaSnmpException($"Sem resposta SNMP ({ex.SocketErrorCode}).", semResposta: true, ex);
        }
        catch (SnmpException ex)
        {
            throw new FalhaSnmpException($"Erro SNMP: {ex.Message}", semResposta: false, ex);
        }
    }

    private static async Task<IPEndPoint> ResolverAsync(AlvoSnmp alvo, CancellationToken ct)
    {
        if (IPAddress.TryParse(alvo.Endereco.Trim(), out var ip))
            return new IPEndPoint(ip, alvo.Porta);

        try
        {
            var enderecos = await Dns.GetHostAddressesAsync(alvo.Endereco.Trim(), ct);
            var escolhido = enderecos.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)
                ?? enderecos.FirstOrDefault();
            if (escolhido is null)
                throw new FalhaSnmpException($"O nome '{alvo.Endereco}' não resolveu para nenhum IP.", semResposta: false);
            return new IPEndPoint(escolhido, alvo.Porta);
        }
        catch (SocketException ex)
        {
            throw new FalhaSnmpException($"Não foi possível resolver o nome '{alvo.Endereco}' (DNS).", semResposta: false, ex);
        }
    }

    private static VersionCode Versao(AlvoSnmp alvo) => alvo.Versao == VersaoSnmp.V1 ? VersionCode.V1 : VersionCode.V2;

    private static ValorSnmp Converter(Variable v)
    {
        var oid = v.Id.ToString();
        return v.Data switch
        {
            Integer32 i => new ValorSnmp(oid, Numero: i.ToInt32()),
            Counter32 c => new ValorSnmp(oid, Numero: c.ToUInt32()),
            Gauge32 g => new ValorSnmp(oid, Numero: g.ToUInt32()),
            Counter64 c => new ValorSnmp(oid, Numero: (long)Math.Min(c.ToUInt64(), long.MaxValue)),
            TimeTicks t => new ValorSnmp(oid, Numero: t.ToUInt32()),
            OctetString s => new ValorSnmp(oid, Texto: InterpretacaoSnmpImpressora.TextoDeOctetos(s.GetRaw()), Bytes: s.GetRaw()),
            NoSuchObject or NoSuchInstance or EndOfMibView or Null => new ValorSnmp(oid),
            _ => new ValorSnmp(oid, Texto: v.Data.ToString()),
        };
    }
}
