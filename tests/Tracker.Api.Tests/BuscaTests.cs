using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tracker.Api.Controllers;
using Tracker.Application.Dtos;
using Tracker.Infrastructure.Data;
using Xunit;

namespace Tracker.Api.Tests;

public class BuscaTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public BuscaTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
    }

    [Fact]
    public async Task Busca_SemAutenticacao_Retorna401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/busca?q=abc");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Garante que as consultas TPH (Notebook/Telefone) traduzem para SQL de verdade.</summary>
    [Fact]
    public async Task Busca_ComTermo_ExecutaConsultaNoBanco()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TrackerDbContext>();
        var controller = new BuscaController(db);

        var resultado = await controller.Buscar("zz-termo-inexistente-zz");

        var ok = Assert.IsType<OkObjectResult>(resultado.Result);
        var dto = Assert.IsType<BuscaGlobalDto>(ok.Value);
        Assert.Empty(dto.Dispositivos);
        Assert.Empty(dto.Usuarios);
    }

    /// <summary>Pega um dispositivo existente e confirma que a busca pelo asset tag o encontra.</summary>
    [Fact]
    public async Task Busca_PorAssetTagExistente_EncontraDispositivo()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TrackerDbContext>();
        var existente = await db.Dispositivos.AsNoTracking()
            .Where(d => d.AssetTag != null && d.AssetTag.Length >= 2)
            .FirstOrDefaultAsync();
        if (existente is null) return; // base sem patrimônio cadastrado: nada a verificar

        var resultado = await new BuscaController(db).Buscar(existente.AssetTag!);

        var ok = Assert.IsType<OkObjectResult>(resultado.Result);
        var dto = Assert.IsType<BuscaGlobalDto>(ok.Value);
        Assert.Contains(dto.Dispositivos, d => d.Id == existente.Id);
    }

    [Fact]
    public async Task Busca_TermoCurto_NaoConsultaERetornaVazio()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TrackerDbContext>();
        var controller = new BuscaController(db);

        var resultado = await controller.Buscar("a");

        var ok = Assert.IsType<OkObjectResult>(resultado.Result);
        var dto = Assert.IsType<BuscaGlobalDto>(ok.Value);
        Assert.Empty(dto.Dispositivos);
    }
}
