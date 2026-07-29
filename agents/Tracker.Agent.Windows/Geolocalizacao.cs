using System.Net.Http;
using System.Net.Http.Json;

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

    public Geolocalizacao(IHttpClientFactory factory, ILogger<Geolocalizacao> logger)
    {
        _http = factory.CreateClient("geo");
        _logger = logger;
    }

    private record IpApiResposta(string? status, double? lat, double? lon, string? city, string? regionName, string? country);

    public async Task<Localizacao?> ObterAsync(CancellationToken ct)
    {
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
