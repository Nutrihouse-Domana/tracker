using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tracker.Domain.Entities;
using Tracker.Domain.Enums;
using Tracker.Infrastructure.Data;
using Xunit;

namespace Tracker.Api.Tests;

/// <summary>
/// Protege o mapeamento TPH do Mobile (celulares e tablets no mesmo tipo).
/// </summary>
public class MobileTphTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public MobileTphTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
    }

    private TrackerDbContext CriarContexto(out IServiceScope escopo)
    {
        escopo = _factory.Services.CreateScope();
        return escopo.ServiceProvider.GetRequiredService<TrackerDbContext>();
    }

    [Fact]
    public void Mobile_EstaMapeadoNoDiscriminadorTph()
    {
        var db = CriarContexto(out var escopo);
        using var _ = escopo;

        var tipo = db.Model.FindEntityType(typeof(Mobile));

        Assert.NotNull(tipo);
        Assert.Equal(TipoDispositivo.Mobile, tipo!.GetDiscriminatorValue());
        // TPH de verdade: mora na mesma tabela dos outros dispositivos.
        Assert.Equal("Dispositivos", tipo.GetTableName());
    }

    /// <summary>
    /// O discriminador é gravado como número. Mobile PRECISA continuar valendo 1 (o antigo
    /// Telefone), senão os aparelhos já cadastrados seriam reinterpretados como outro tipo.
    /// </summary>
    [Fact]
    public void Mobile_MantemOValorNumericoDoAntigoTelefone()
    {
        Assert.Equal(1, (int)TipoDispositivo.Mobile);
        Assert.Equal(2, (int)TipoDispositivo.Periferico);
    }

    [Fact]
    public void Mobile_TemImeiOpcionalNaColunaImei()
    {
        var db = CriarContexto(out var escopo);
        using var _ = escopo;

        var imei = db.Model.FindEntityType(typeof(Mobile))!.FindProperty(nameof(Mobile.Imei))!;

        Assert.Equal("Imei", imei.GetColumnName());
        // Tablet só-Wi-Fi não tem IMEI: a coluna precisa aceitar nulo.
        Assert.True(imei.IsNullable);
    }

    /// <summary>
    /// Set&lt;Mobile&gt;() é o caminho usado pelo AgentAuth para autenticar o agente Android; se
    /// não traduzir para SQL, nenhum aparelho consegue fazer heartbeat.
    /// </summary>
    [Fact]
    public async Task ConsultaPorMobile_TraduzParaSql()
    {
        var db = CriarContexto(out var escopo);
        using var _ = escopo;
        if (!db.Database.CanConnect()) return; // sem banco disponível (CI): nada a verificar

        var encontrado = await db.Set<Mobile>()
            .FirstOrDefaultAsync(m => m.TokenAgenteHash == "hash-inexistente-para-teste");

        Assert.Null(encontrado);
    }
}
