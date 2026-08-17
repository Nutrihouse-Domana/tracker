using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tracker.Domain.Entities;
using Tracker.Infrastructure.Data;
using Xunit;

namespace Tracker.Api.Tests;

/// <summary>
/// Protege o reencontro do ativo no auto-cadastro.
///
/// O caso real que originou estes testes: sem Device Owner o agente Android identifica o aparelho
/// por um valor derivado do ANDROID_ID, que o sistema troca quando o APK é reinstalado com outra
/// chave de assinatura. Ao trocar o agente, o mesmo tablet voltou como dispositivo novo e o
/// inventário ficou com o ativo duplicado. O patrimônio é o identificador que atravessa isso.
/// </summary>
public class AutoEnrollmentTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string ChaveDeTeste = "chave-de-teste-do-auto-enrollment";

    private readonly WebApplicationFactory<Program> _factory;

    public AutoEnrollmentTests(WebApplicationFactory<Program> factory)
    {
        // A chave da empresa vem de user-secrets em desenvolvimento e de variável de ambiente em
        // produção; fixá-la aqui deixa o teste independente da máquina onde roda.
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Enrollment:CompanyKey"] = ChaveDeTeste,
                }));
        });
    }

    private HttpClient CriarCliente()
    {
        var cliente = _factory.CreateClient();
        cliente.DefaultRequestHeaders.Add("X-Enrollment-Key", ChaveDeTeste);
        return cliente;
    }

    private static async Task<(HttpStatusCode status, JsonElement corpo)> RegistrarAsync(
        HttpClient cliente, string numeroSerie, string? assetTag = null, string? imei = null)
    {
        var resposta = await cliente.PostAsJsonAsync("/api/agente/auto-enrollment", new
        {
            hostname = "tablet-de-teste",
            numeroSerie,
            sistemaOperacional = "Android",
            versaoSO = "Android 11 (API 30)",
            macAddress = (string?)null,
            tipo = "Mobile",
            imei,
            assetTag,
        });

        var corpo = resposta.IsSuccessStatusCode
            ? await resposta.Content.ReadFromJsonAsync<JsonElement>()
            : default;
        return (resposta.StatusCode, corpo);
    }

    /// <summary>
    /// O coração da correção: trocar o agente muda o identificador provisório, mas o patrimônio
    /// digitado pelo técnico reencontra o cadastro em vez de criar um segundo ativo.
    /// </summary>
    [Fact]
    public async Task Registrar_ComMesmoPatrimonio_ReaproveitaOCadastroEmVezDeDuplicar()
    {
        using var escopo = _factory.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<TrackerDbContext>();
        if (!db.Database.CanConnect()) return; // sem banco disponível (CI): nada a verificar

        var patrimonio = $"PAT-{Guid.NewGuid():N}"[..20];
        var serieAntiga = $"ANDROID-{Guid.NewGuid():N}";
        var serieNova = $"ANDROID-{Guid.NewGuid():N}"; // o que muda ao reinstalar com outra chave

        var original = new Mobile
        {
            NumeroSerie = serieAntiga,
            AssetTag = patrimonio,
            Hostname = "tablet-de-teste",
            SistemaOperacional = "Android",
            DataAquisicao = DateTime.UtcNow,
        };
        db.Dispositivos.Add(original);
        await db.SaveChangesAsync();

        try
        {
            var (status, corpo) = await RegistrarAsync(CriarCliente(), serieNova, patrimonio);

            Assert.Equal(HttpStatusCode.OK, status);
            // Mesmo Id: é o mesmo ativo, com todo o histórico preservado.
            Assert.Equal(original.Id, corpo.GetProperty("dispositivoId").GetGuid());

            var quantos = await db.Dispositivos.IgnoreQueryFilters()
                .CountAsync(d => d.AssetTag == patrimonio);
            Assert.Equal(1, quantos);

            // Os dois identificadores são provisórios, então o registro passa a valer pelo atual.
            await db.Entry(original).ReloadAsync();
            Assert.Equal(serieNova, original.NumeroSerie);
        }
        finally
        {
            db.Dispositivos.Remove(original);
            await db.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Um serial de fábrica já registrado não pode ser substituído pelo identificador provisório
    /// do app: seria perder o dado bom e trocá-lo por um que muda a cada reinstalação.
    /// </summary>
    [Fact]
    public async Task Registrar_NaoRebaixaSerialDeFabricaParaOProvisorio()
    {
        using var escopo = _factory.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<TrackerDbContext>();
        if (!db.Database.CanConnect()) return;

        var patrimonio = $"PAT-{Guid.NewGuid():N}"[..20];
        var serialDeFabrica = $"HA1M{Guid.NewGuid():N}"[..12];

        var original = new Mobile
        {
            NumeroSerie = serialDeFabrica,
            AssetTag = patrimonio,
            Hostname = "tablet-de-teste",
            SistemaOperacional = "Android",
            DataAquisicao = DateTime.UtcNow,
        };
        db.Dispositivos.Add(original);
        await db.SaveChangesAsync();

        try
        {
            var (status, _) = await RegistrarAsync(
                CriarCliente(), $"ANDROID-{Guid.NewGuid():N}", patrimonio);

            Assert.Equal(HttpStatusCode.OK, status);

            await db.Entry(original).ReloadAsync();
            Assert.Equal(serialDeFabrica, original.NumeroSerie);
        }
        finally
        {
            db.Dispositivos.Remove(original);
            await db.SaveChangesAsync();
        }
    }

    /// <summary>
    /// O patrimônio provisório (AUTO-*) do auto-cadastro cede lugar ao real assim que o técnico
    /// o digita — sem isso o inventário ficaria para sempre com etiquetas inventadas.
    /// </summary>
    [Fact]
    public async Task Registrar_PromovePatrimonioProvisorioParaODigitado()
    {
        using var escopo = _factory.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<TrackerDbContext>();
        if (!db.Database.CanConnect()) return;

        var serie = $"ANDROID-{Guid.NewGuid():N}";
        var patrimonioReal = $"PAT-{Guid.NewGuid():N}"[..20];

        var original = new Mobile
        {
            NumeroSerie = serie,
            AssetTag = $"AUTO-{serie}",
            Hostname = "tablet-de-teste",
            SistemaOperacional = "Android",
            DataAquisicao = DateTime.UtcNow,
        };
        db.Dispositivos.Add(original);
        await db.SaveChangesAsync();

        try
        {
            // Casa pelo número de série; o patrimônio digitado substitui o provisório.
            var (status, corpo) = await RegistrarAsync(CriarCliente(), serie, patrimonioReal);

            Assert.Equal(HttpStatusCode.OK, status);
            Assert.Equal(original.Id, corpo.GetProperty("dispositivoId").GetGuid());

            await db.Entry(original).ReloadAsync();
            Assert.Equal(patrimonioReal, original.AssetTag);
        }
        finally
        {
            db.Dispositivos.Remove(original);
            await db.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Aparelho realmente novo continua entrando no inventário — a correção não pode transformar
    /// todo registro em reaproveitamento.
    /// </summary>
    [Fact]
    public async Task Registrar_AparelhoDesconhecido_CriaOCadastro()
    {
        using var escopo = _factory.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<TrackerDbContext>();
        if (!db.Database.CanConnect()) return;

        var serie = $"ANDROID-{Guid.NewGuid():N}";
        var patrimonio = $"PAT-{Guid.NewGuid():N}"[..20];

        var (status, corpo) = await RegistrarAsync(CriarCliente(), serie, patrimonio);
        Assert.Equal(HttpStatusCode.OK, status);

        var id = corpo.GetProperty("dispositivoId").GetGuid();
        var criado = await db.Dispositivos.IgnoreQueryFilters().FirstOrDefaultAsync(d => d.Id == id);

        try
        {
            Assert.NotNull(criado);
            // O patrimônio digitado vale desde o cadastro, em vez do AUTO-* provisório.
            Assert.Equal(patrimonio, criado!.AssetTag);
        }
        finally
        {
            if (criado is not null)
            {
                db.Dispositivos.Remove(criado);
                await db.SaveChangesAsync();
            }
        }
    }
}
