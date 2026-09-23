using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Tracker.Domain.Entities;
using Tracker.Infrastructure.Data;
using Xunit;

namespace Tracker.Api.Tests;

/// <summary>
/// Protege o modelo da unidade, da posse (usuário x local) e da exclusão lógica de dispositivos.
/// </summary>
public class UnidadeEPosseTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public UnidadeEPosseTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
    }

    private TrackerDbContext CriarContexto(out IServiceScope escopo)
    {
        escopo = _factory.Services.CreateScope();
        return escopo.ServiceProvider.GetRequiredService<TrackerDbContext>();
    }

    /// <summary>
    /// Sem o filtro global, um dispositivo excluído voltaria a aparecer em listas, busca e
    /// dashboard — cada consulta teria que lembrar de filtrar sozinha.
    /// </summary>
    [Fact]
    public void Dispositivo_TemFiltroGlobalDeExclusaoLogica()
    {
        var db = CriarContexto(out var escopo);
        using var _ = escopo;

        var tipo = db.Model.FindEntityType(typeof(Dispositivo));

        Assert.NotNull(tipo!.GetQueryFilter());
    }

    /// <summary>
    /// O acervo já cadastrado segue o fluxo normal (detentor nominal). Se o padrão virasse false,
    /// todo dispositivo existente passaria a "ativo do local" — sem unidade — e sumiria o dono.
    /// </summary>
    [Fact]
    public void Dispositivo_NasceComPosseDeUsuario()
    {
        Assert.True(new Notebook().PertenceAUsuario);
        Assert.True(new Mobile().PertenceAUsuario);
        Assert.True(new Periferico().PertenceAUsuario);
    }

    /// <summary>
    /// Identificação interna (rótulo manual da TI) é OPCIONAL: nasce nula e o banco a aceita nula.
    /// Se virasse obrigatória, todo dispositivo já cadastrado quebraria — não há backfill.
    /// </summary>
    [Fact]
    public void IdentificacaoInterna_EhOpcional()
    {
        Assert.Null(new Notebook().IdentificacaoInterna);
        Assert.Null(new Mobile().IdentificacaoInterna);
        Assert.Null(new Periferico().IdentificacaoInterna);

        var db = CriarContexto(out var escopo);
        using var _ = escopo;

        var prop = db.Model.FindEntityType(typeof(Dispositivo))!
            .FindProperty(nameof(Dispositivo.IdentificacaoInterna));

        Assert.NotNull(prop);
        Assert.True(prop!.IsNullable);
    }

    /// <summary>Apagar uma unidade não pode deixar dispositivos apontando para o vazio.</summary>
    [Fact]
    public void Dispositivo_ReferenciaUnidadeComDeleteRestrict()
    {
        var db = CriarContexto(out var escopo);
        using var _ = escopo;

        var fk = db.Model.FindEntityType(typeof(Dispositivo))!
            .GetForeignKeys()
            .Single(f => f.PrincipalEntityType.ClrType == typeof(Unidade));

        Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior);
        Assert.True(fk.Properties.Single().IsNullable); // unidade é opcional
    }

    /// <summary>
    /// A exclusão definitiva conta com o banco apagando os dependentes em cascata; só o termo de
    /// uso é Restrict (prova documental) e por isso o controller o remove à mão. Se alguém trocar
    /// um desses comportamentos, o DELETE passa a estourar com erro cru de FK.
    /// </summary>
    [Fact]
    public void DependentesDoDispositivo_CascateiamMenosOTermoDeUso()
    {
        var db = CriarContexto(out var escopo);
        using var _ = escopo;

        var paraDispositivo = db.Model.GetEntityTypes()
            .SelectMany(e => e.GetForeignKeys())
            .Where(f => f.PrincipalEntityType.ClrType == typeof(Dispositivo))
            .ToList();

        var termo = paraDispositivo.Single(f => f.DeclaringEntityType.ClrType == typeof(TermoDeUso));
        Assert.Equal(DeleteBehavior.Restrict, termo.DeleteBehavior);

        foreach (var fk in paraDispositivo.Where(f => f.DeclaringEntityType.ClrType != typeof(TermoDeUso)))
            Assert.Equal(DeleteBehavior.Cascade, fk.DeleteBehavior);
    }

    /// <summary>A consulta filtrada e o IgnoreQueryFilters precisam traduzir para SQL de verdade.</summary>
    [Fact]
    public async Task ConsultaDeArquivados_TraduzParaSql()
    {
        var db = CriarContexto(out var escopo);
        using var _ = escopo;
        if (!db.Database.CanConnect()) return; // sem banco disponível (CI): nada a verificar

        var arquivados = await db.Dispositivos.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(d => d.ExcluidoEm != null)
            .CountAsync();

        var ativos = await db.Dispositivos.AsNoTracking().CountAsync();

        Assert.True(arquivados >= 0);
        Assert.True(ativos >= 0);
    }
}
