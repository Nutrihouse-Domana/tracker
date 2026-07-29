using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace Tracker.Agent.Windows;

/// <summary>
/// Garante que o agente tenha um token de dispositivo antes de operar. Ordem de resolução:
///   1. token.dat salvo (auto-cadastro anterior);
///   2. AgentToken preenchido manualmente no appsettings;
///   3. auto-cadastro usando a EnrollmentKey (chave da empresa) — cria o dispositivo no backend.
/// </summary>
public class EnrollmentService
{
    private readonly IHttpClientFactory _factory;
    private readonly AgenteOptions _options;
    private readonly TokenHolder _tokenHolder;
    private readonly ILogger<EnrollmentService> _logger;
    private readonly string _tokenPath = Path.Combine(AppContext.BaseDirectory, "token.dat");

    public EnrollmentService(
        IHttpClientFactory factory, IOptions<AgenteOptions> options, TokenHolder tokenHolder, ILogger<EnrollmentService> logger)
    {
        _factory = factory;
        _options = options.Value;
        _tokenHolder = tokenHolder;
        _logger = logger;
    }

    private record EnrollmentResp(Guid dispositivoId, string token);

    public async Task<bool> GarantirTokenAsync(CancellationToken ct)
    {
        // 1. token já salvo em disco
        if (File.Exists(_tokenPath))
        {
            _tokenHolder.Token = (await File.ReadAllTextAsync(_tokenPath, ct)).Trim();
            _logger.LogInformation("Token carregado de token.dat.");
            return true;
        }

        // 2. token manual no appsettings
        if (!string.IsNullOrWhiteSpace(_options.AgentToken))
        {
            _tokenHolder.Token = _options.AgentToken.Trim();
            _logger.LogInformation("Usando AgentToken configurado manualmente.");
            return true;
        }

        // 3. auto-cadastro com a chave da empresa
        if (string.IsNullOrWhiteSpace(_options.EnrollmentKey))
        {
            _logger.LogError("Sem AgentToken e sem EnrollmentKey — não é possível autenticar o agente.");
            return false;
        }

        try
        {
            var http = _factory.CreateClient("backend");
            http.BaseAddress = new Uri(_options.BackendBaseUrl);
            http.DefaultRequestHeaders.Add("X-Enrollment-Key", _options.EnrollmentKey);

            var body = new
            {
                hostname = Environment.MachineName,
                numeroSerie = InfoMaquina.NumeroSerie(),
                sistemaOperacional = InfoMaquina.SistemaOperacional(),
                versaoSO = Environment.OSVersion.Version.ToString(),
                macAddress = InfoMaquina.MacAddress(),
                tipo = _options.Tipo,
                imei = (string?)null,
            };

            var resp = await http.PostAsJsonAsync("/api/agente/auto-enrollment", body, ct);
            resp.EnsureSuccessStatusCode();
            var dados = await resp.Content.ReadFromJsonAsync<EnrollmentResp>(cancellationToken: ct);

            if (dados is null || string.IsNullOrWhiteSpace(dados.token))
            {
                _logger.LogError("Auto-cadastro não retornou token.");
                return false;
            }

            await File.WriteAllTextAsync(_tokenPath, dados.token, ct);
            _tokenHolder.Token = dados.token;
            _logger.LogInformation("Auto-cadastro concluído. Dispositivo {Id} registrado no inventário.", dados.dispositivoId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha no auto-cadastro. Verifique EnrollmentKey e a URL do backend.");
            return false;
        }
    }
}
