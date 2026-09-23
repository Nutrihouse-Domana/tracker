using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tracker.Domain.Entities;
using Tracker.Infrastructure.Data;
using Tracker.Infrastructure.Security;
using Xunit;

namespace Tracker.Api.Tests;

/// <summary>
/// Protege a atualização OTA do agente Android: modelo do pacote, oferta no heartbeat e as
/// portas de entrada (download autenticado por token, publicação só para ADM logado).
/// </summary>
public class AtualizacaoOtaTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AtualizacaoOtaTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
    }

    private TrackerDbContext CriarContexto(out IServiceScope escopo)
    {
        escopo = _factory.Services.CreateScope();
        return escopo.ServiceProvider.GetRequiredService<TrackerDbContext>();
    }

    [Fact]
    public void PacoteAgenteAndroid_TemIndiceUnicoDeVersaoCode()
    {
        var db = CriarContexto(out var escopo);
        using var _ = escopo;

        var tipo = db.Model.FindEntityType(typeof(PacoteAgenteAndroid));

        Assert.NotNull(tipo);
        var indice = tipo!.GetIndexes()
            .Single(i => i.Properties.Single().Name == nameof(PacoteAgenteAndroid.VersaoCode));
        // Dois pacotes com o mesmo versionCode deixariam a "versão vigente" ambígua.
        Assert.True(indice.IsUnique);
    }

    /// <summary>
    /// O agente Windows não envia versionCode: a coluna precisa aceitar nulo, e nulo significa
    /// "nunca oferecer APK" — não "versão zero".
    /// </summary>
    [Fact]
    public void DispositivoComAgente_TemVersaoCodeOpcional()
    {
        var db = CriarContexto(out var escopo);
        using var _ = escopo;

        var propriedade = db.Model.FindEntityType(typeof(Notebook))!
            .FindProperty(nameof(DispositivoComAgente.AgenteVersaoCode));

        Assert.NotNull(propriedade);
        Assert.True(propriedade!.IsNullable);
    }

    /// <summary>Sem token de agente o APK não sai do servidor.</summary>
    [Fact]
    public async Task DownloadDoApk_ExigeTokenDeAgente()
    {
        using (var escopo = _factory.Services.CreateScope())
        {
            var db = escopo.ServiceProvider.GetRequiredService<TrackerDbContext>();
            if (!db.Database.CanConnect()) return; // sem banco (CI): a autenticação consulta o banco
        }

        var cliente = _factory.CreateClient();

        var resposta = await cliente.GetAsync("/api/agente/atualizacao/apk");

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    /// <summary>Publicar um APK para a frota é rota de ADM: anônimo recebe 401.</summary>
    [Fact]
    public async Task PublicarPacote_ExigeAdmLogado()
    {
        var cliente = _factory.CreateClient();

        using var form = new MultipartFormDataContent { { new StringContent("2"), "versaoCode" } };
        var resposta = await cliente.PostAsync("/api/agente-android/pacotes", form);

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    /// <summary>
    /// O coração da OTA: um agente atrás da versão publicada recebe a oferta no heartbeat; um
    /// agente em dia (ou que não informa versionCode, caso do Windows) não recebe nada.
    /// </summary>
    [Fact]
    public async Task Heartbeat_OfereceAtualizacaoSoParaAgenteDesatualizado()
    {
        var db = CriarContexto(out var escopo);
        using var _ = escopo;
        if (!db.Database.CanConnect()) return; // sem banco disponível (CI): nada a verificar

        var tokenService = _factory.Services.GetRequiredService<AgentTokenService>();
        var (token, hash) = tokenService.Gerar();

        // versionCode acima de qualquer publicado de verdade, para o teste não depender do estado.
        var maiorExistente = await db.PacotesAgenteAndroid
            .OrderByDescending(p => p.VersaoCode)
            .Select(p => (int?)p.VersaoCode)
            .FirstOrDefaultAsync() ?? 0;
        var versaoPublicada = Math.Max(maiorExistente, 1_000_000) + 1;

        var dispositivo = new Mobile
        {
            NumeroSerie = $"OTA-TESTE-{Guid.NewGuid():N}",
            AssetTag = $"OTA-TESTE-{Guid.NewGuid():N}",
            Hostname = "aparelho-teste-ota",
            SistemaOperacional = "Android",
            DataAquisicao = DateTime.UtcNow,
            TokenAgenteHash = hash,
        };
        var pacote = new PacoteAgenteAndroid
        {
            VersaoCode = versaoPublicada,
            VersaoNome = "teste-ota",
            NomeArquivoOriginal = "teste.apk",
            CaminhoRelativo = "teste-inexistente.apk",
            HashSha256 = new string('0', 64),
            TamanhoBytes = 123,
            EnviadoPorAdmId = Guid.NewGuid(),
        };
        db.Dispositivos.Add(dispositivo);
        db.PacotesAgenteAndroid.Add(pacote);
        await db.SaveChangesAsync();

        try
        {
            var cliente = _factory.CreateClient();
            cliente.DefaultRequestHeaders.Add("X-Agent-Token", token);

            // Agente desatualizado: recebe a oferta com o hash para conferência.
            var desatualizado = await EnviarHeartbeat(cliente, versaoPublicada - 1);
            var oferta = desatualizado.GetProperty("atualizacaoAgente");
            Assert.Equal(versaoPublicada, oferta.GetProperty("versaoCode").GetInt32());
            Assert.Equal(pacote.HashSha256, oferta.GetProperty("hashSha256").GetString());

            // Agente em dia: nada a fazer.
            var emDia = await EnviarHeartbeat(cliente, versaoPublicada);
            Assert.True(EstaSemOferta(emDia));

            // Sem versionCode (agente Windows): nunca recebe oferta de APK.
            var semVersao = await EnviarHeartbeat(cliente, agenteVersaoCode: null);
            Assert.True(EstaSemOferta(semVersao));
        }
        finally
        {
            db.PacotesAgenteAndroid.Remove(pacote);
            db.Dispositivos.Remove(dispositivo);
            await db.SaveChangesAsync();
        }
    }

    private static async Task<JsonElement> EnviarHeartbeat(HttpClient cliente, int? agenteVersaoCode)
    {
        var resposta = await cliente.PostAsJsonAsync("/api/agente/heartbeat", new
        {
            hostname = "aparelho-teste-ota",
            agenteVersao = "teste",
            agenteVersaoCode,
        });

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        return await resposta.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static bool EstaSemOferta(JsonElement heartbeat) =>
        !heartbeat.TryGetProperty("atualizacaoAgente", out var oferta) ||
        oferta.ValueKind == JsonValueKind.Null;
}
