using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tracker.Api.Controllers;
using Tracker.Application.Dtos;
using Tracker.Domain.Entities;
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

    /// <summary>
    /// Abre um escopo com o DbContext, ou devolve null quando o banco não está acessível.
    /// O pipeline de deploy roda os testes contra uma instância inexistente de propósito
    /// (para nunca tocar produção): nesses casos os testes que dependem do banco são pulados.
    /// </summary>
    private (IServiceScope Escopo, TrackerDbContext Db)? AbrirBanco()
    {
        var escopo = _factory.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<TrackerDbContext>();
        if (db.Database.CanConnect()) return (escopo, db);

        escopo.Dispose();
        return null;
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
        var banco = AbrirBanco();
        if (banco is null) return; // sem banco disponível (CI): nada a verificar
        using var scope = banco.Value.Escopo;
        var controller = new BuscaController(banco.Value.Db);

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
        var banco = AbrirBanco();
        if (banco is null) return; // sem banco disponível (CI): nada a verificar
        using var scope = banco.Value.Escopo;
        var db = banco.Value.Db;
        var existente = await db.Dispositivos.AsNoTracking()
            .Where(d => d.AssetTag != null && d.AssetTag.Length >= 2)
            .FirstOrDefaultAsync();
        if (existente is null) return; // base sem patrimônio cadastrado: nada a verificar

        var resultado = await new BuscaController(db).Buscar(existente.AssetTag!);

        var ok = Assert.IsType<OkObjectResult>(resultado.Result);
        var dto = Assert.IsType<BuscaGlobalDto>(ok.Value);
        Assert.Contains(dto.Dispositivos, d => d.Id == existente.Id);
    }

    /// <summary>
    /// A TI procura os ativos pelo rótulo manual (ex.: MAQ-0123): ele precisa achar o aparelho e
    /// voltar no resultado, inclusive na impressora, que tem consulta própria para os demais campos.
    /// </summary>
    [Fact]
    public async Task Busca_PorIdentificacaoInterna_EncontraEDevolveORotulo()
    {
        var banco = AbrirBanco();
        if (banco is null) return; // sem banco disponível (CI): nada a verificar
        using var scope = banco.Value.Escopo;
        var db = banco.Value.Db;

        var sufixo = Guid.NewGuid().ToString("N")[..10];
        var notebook = new Notebook
        {
            NumeroSerie = $"NB-{Guid.NewGuid():N}", IdentificacaoInterna = $"MAQ-{sufixo}",
            Hostname = "nb-busca", SistemaOperacional = "Windows", DataAquisicao = DateTime.UtcNow,
        };
        var impressora = new Impressora
        {
            NumeroSerie = $"IMP-{Guid.NewGuid():N}", IdentificacaoInterna = $"IMP-{sufixo}",
            PertenceAUsuario = false, DataAquisicao = DateTime.UtcNow,
        };
        db.Dispositivos.AddRange(notebook, impressora);
        await db.SaveChangesAsync();

        try
        {
            var dto = Assert.IsType<BuscaGlobalDto>(
                Assert.IsType<OkObjectResult>((await new BuscaController(db).Buscar(sufixo)).Result).Value);

            Assert.Contains(dto.Dispositivos, d => d.Id == notebook.Id && d.IdentificacaoInterna == $"MAQ-{sufixo}");
            Assert.Contains(dto.Dispositivos, d => d.Id == impressora.Id && d.IdentificacaoInterna == $"IMP-{sufixo}");
        }
        finally
        {
            db.Dispositivos.RemoveRange(notebook, impressora);
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task Busca_TermoCurto_NaoConsultaERetornaVazio()
    {
        // Termo curto nem chega ao banco: não precisa de SQL Server disponível.
        using var scope = _factory.Services.CreateScope();
        var controller = new BuscaController(scope.ServiceProvider.GetRequiredService<TrackerDbContext>());

        var resultado = await controller.Buscar("a");

        var ok = Assert.IsType<OkObjectResult>(resultado.Result);
        var dto = Assert.IsType<BuscaGlobalDto>(ok.Value);
        Assert.Empty(dto.Dispositivos);
    }
}
