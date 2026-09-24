using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Tracker.Agent.Windows;

/// <summary>
/// Comando remoto. <see cref="TipoComando"/> chega como texto ("Bloquear") pelo heartbeat e,
/// com o servidor bem configurado, também pelo hub — mas o SignalR já entregou o enum como
/// NÚMERO quando o <c>AddJsonProtocol</c> faltava, e o comando era descartado em silêncio.
/// O conversor aceita os dois formatos como defesa.
/// </summary>
public record ComandoPendente(
    Guid Id,
    [property: JsonConverter(typeof(TipoComandoTolerante))] string TipoComando);

/// <summary>
/// Lê <c>tipoComando</c> como texto ou como número do enum <c>TipoComandoRemoto</c> do backend
/// (Bloquear = 0, Desbloquear = 1 — a ordem do enum em Tracker.Domain). Número desconhecido vira
/// o próprio número em texto, que o executor ignora.
/// </summary>
public sealed class TipoComandoTolerante : JsonConverter<string>
{
    private static readonly string[] PorNumero = ["Bloquear", "Desbloquear"];

    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                return reader.GetString() ?? string.Empty;
            case JsonTokenType.Number when reader.TryGetInt32(out var n):
                return n >= 0 && n < PorNumero.Length ? PorNumero[n] : n.ToString();
            case JsonTokenType.Null:
                return string.Empty;
            default:
                throw new JsonException($"tipoComando em formato inesperado: {reader.TokenType}.");
        }
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value);
}
public record HeartbeatResposta(List<ComandoPendente> ComandosPendentes);

/// <summary>O backend rejeitou o token (401). Sinaliza ao Worker que é hora de re-cadastrar.</summary>
public class TokenInvalidoException : Exception;

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
        if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized) throw new TokenInvalidoException();
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
