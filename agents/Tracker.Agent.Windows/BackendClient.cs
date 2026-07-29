using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace Tracker.Agent.Windows;

public record ComandoPendente(Guid Id, string TipoComando);
public record HeartbeatResposta(List<ComandoPendente> ComandosPendentes);

/// <summary>
/// Comunicação REST com o backend. O token é lido do TokenHolder a cada requisição, pois ele
/// só é conhecido após a resolução/auto-cadastro no início da execução.
/// </summary>
public class BackendClient
{
    private readonly IHttpClientFactory _factory;
    private readonly AgenteOptions _options;
    private readonly TokenHolder _tokenHolder;

    public BackendClient(IHttpClientFactory factory, IOptions<AgenteOptions> options, TokenHolder tokenHolder)
    {
        _factory = factory;
        _options = options.Value;
        _tokenHolder = tokenHolder;
    }

    private HttpRequestMessage Requisicao(HttpMethod metodo, string rota, object? corpo = null)
    {
        var req = new HttpRequestMessage(metodo, new Uri(new Uri(_options.BackendBaseUrl), rota));
        req.Headers.Add("X-Agent-Token", _tokenHolder.Token ?? string.Empty);
        if (corpo is not null) req.Content = JsonContent.Create(corpo);
        return req;
    }

    public async Task<HeartbeatResposta?> EnviarHeartbeatAsync(Localizacao? loc, string agenteVersao, CancellationToken ct)
    {
        var corpo = new
        {
            hostname = Environment.MachineName,
            agenteVersao,
            latitude = loc?.Latitude,
            longitude = loc?.Longitude,
            enderecoAproximado = loc?.Endereco,
            precisaoMetros = loc is null ? (double?)null : 5000,
        };

        var http = _factory.CreateClient("backend");
        var resp = await http.SendAsync(Requisicao(HttpMethod.Post, "/api/agente/heartbeat", corpo), ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<HeartbeatResposta>(cancellationToken: ct);
    }

    public async Task ConfirmarComandoAsync(Guid comandoId, CancellationToken ct)
    {
        var http = _factory.CreateClient("backend");
        var resp = await http.SendAsync(Requisicao(HttpMethod.Post, $"/api/agente/comandos/{comandoId}/confirmar"), ct);
        resp.EnsureSuccessStatusCode();
    }
}
