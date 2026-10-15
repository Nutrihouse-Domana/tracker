using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace Tracker.Agent.Windows;

public record Localizacao(double Latitude, double Longitude, string? Endereco);

/// <summary>
/// Estima a localização do notebook a partir do IP público, via serviço ip-api.com
/// (gratuito, sem chave). Notebooks não têm GPS — precisão em nível de cidade/região.
/// </summary>
public class Geolocalizacao
{
    private readonly HttpClient _http;
    private readonly ILogger<Geolocalizacao> _logger;
    private readonly TimeSpan _intervalo;

    /// <summary>Momento da última TENTATIVA (com sucesso ou não): falha também espera o intervalo,
    /// senão um IP já limitado pelo ip-api seria martelado a cada heartbeat.</summary>
    private DateTime _ultimaConsultaUtc = DateTime.MinValue;

    public Geolocalizacao(IHttpClientFactory factory, ILogger<Geolocalizacao> logger, IOptions<AgenteOptions> options)
    {
        _http = factory.CreateClient("geo");
        _logger = logger;
        _intervalo = TimeSpan.FromSeconds(Math.Max(60, options.Value.LocalizacaoIntervalSeconds));
    }

    private record IpApiResposta(string? status, double? lat, double? lon, string? city, string? regionName, string? country);

    /// <summary>
    /// Devolve a localização no máximo uma vez por <see cref="AgenteOptions.LocalizacaoIntervalSeconds"/>;
    /// nos demais heartbeats devolve null (o heartbeat vai sem localização, e o servidor não grava ponto).
    /// </summary>
    public async Task<Localizacao?> ObterAsync(CancellationToken ct)
    {
        var agora = DateTime.UtcNow;
        if (agora - _ultimaConsultaUtc < _intervalo) return null;
        _ultimaConsultaUtc = agora;

        try
        {
            var r = await _http.GetFromJsonAsync<IpApiResposta>(
                "http://ip-api.com/json/?fields=status,lat,lon,city,regionName,country", ct);

            if (r?.status != "success" || r.lat is null || r.lon is null)
                return null;

            var endereco = string.Join(", ", new[] { r.city, r.regionName, r.country }
                .Where(s => !string.IsNullOrWhiteSpace(s)));
            return new Localizacao(r.lat.Value, r.lon.Value, endereco);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao obter geolocalização por IP.");
            return null;
        }
    }
}
