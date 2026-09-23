using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Tracker.Application.Seguranca;
using Tracker.Domain.Entities;
using Tracker.Infrastructure.Data;
using Tracker.Infrastructure.Identity;
using Xunit;

namespace Tracker.Api.Tests;

/// <summary>
/// Protege os invariantes do controle de acesso por permissão e da trilha de auditoria.
/// </summary>
public class PermissoesEAuditoriaTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public PermissoesEAuditoriaTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
    }

    /// <summary>
    /// Toda permissão do catálogo precisa ter uma policy homônima registrada — senão um
    /// [Authorize(Policy = ...)] com esse valor lançaria em tempo de execução (policy não encontrada).
    /// </summary>
    [Fact]
    public async Task TodaPermissao_TemPolicyRegistrada()
    {
        using var escopo = _factory.Services.CreateScope();
        var provider = escopo.ServiceProvider.GetRequiredService<IAuthorizationPolicyProvider>();

        foreach (var permissao in Permissoes.Todas)
        {
            var policy = await provider.GetPolicyAsync(permissao);
            Assert.NotNull(policy);
        }
    }

    /// <summary>Todo valor do catálogo tem metadados (rótulo/descrição/grupo) para a UI.</summary>
    [Fact]
    public void CatalogoCobreTodasAsPermissoes()
    {
        Assert.NotEmpty(Permissoes.Todas);
        foreach (var permissao in Permissoes.Todas)
            Assert.Contains(Permissoes.Catalogo, d => d.Valor == permissao);
    }

    /// <summary>
    /// LogAuditoria NÃO pode ter FK para Dispositivo: o registro "excluiu o dispositivo X" precisa
    /// sobreviver à exclusão definitiva do alvo (EntidadeId é um Guid solto, de propósito).
    /// </summary>
    [Fact]
    public void LogAuditoria_NaoTemChaveEstrangeira()
    {
        var db = CriarContexto(out var escopo);
        using var _ = escopo;

        var tipo = db.Model.FindEntityType(typeof(LogAuditoria));
        Assert.NotNull(tipo);
        Assert.Empty(tipo!.GetForeignKeys());
    }

    /// <summary>Login nasce ativo: desativar é uma ação explícita, nunca o padrão.</summary>
    [Fact]
    public void UsuarioAdm_NasceAtivo()
    {
        Assert.True(new UsuarioADM().Ativo);
    }

    private TrackerDbContext CriarContexto(out IServiceScope escopo)
    {
        escopo = _factory.Services.CreateScope();
        return escopo.ServiceProvider.GetRequiredService<TrackerDbContext>();
    }
}
