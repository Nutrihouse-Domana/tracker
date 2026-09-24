using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tracker.Domain.Entities;
using Tracker.Domain.Enums;
using Tracker.Infrastructure.Data;
using Tracker.Infrastructure.Security;
using Xunit;

namespace Tracker.Api.Tests;

/// <summary>
/// Protege o ciclo do bloqueio remoto pelo lado do agente (heartbeat e confirmação) e o
/// auto-cadastro com serial genérico de BIOS. Os testes com banco pulam quando não há SQL Server.
/// </summary>
public class BloqueioRemotoTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string ChaveDeTeste = "chave-de-teste-do-bloqueio";

    private readonly WebApplicationFactory<Program> _factory;

    public BloqueioRemotoTests(WebApplicationFactory<Program> factory)
    {
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

    /// <summary>Cria um notebook com token de agente; devolve o token em texto.</summary>
    private async Task<(Notebook dispositivo, string token)> CriarNotebookComAgenteAsync(
        TrackerDbContext db, StatusDispositivo status, Guid? usuarioId = null)
    {
        var (token, hash) = _factory.Services.GetRequiredService<AgentTokenService>().Gerar();
        var notebook = new Notebook
        {
            NumeroSerie = $"BLOQ-TESTE-{Guid.NewGuid():N}",
            AssetTag = $"BLOQ-TESTE-{Guid.NewGuid():N}",
            Hostname = "notebook-teste-bloqueio",
            SistemaOperacional = "Windows",
            DataAquisicao = DateTime.UtcNow,
            Status = status,
            UsuarioAtualId = usuarioId,
            TokenAgenteHash = hash,
        };
        db.Dispositivos.Add(notebook);
        await db.SaveChangesAsync();
        return (notebook, token);
    }

    private static ComandoRemoto Comando(Guid dispositivoId, TipoComandoRemoto tipo, StatusComandoRemoto status, DateTime emissao) =>
        new()
        {
            DispositivoId = dispositivoId,
            TipoComando = tipo,
            Status = status,
            DataEmissao = emissao,
            EmitidoPorAdmId = Guid.NewGuid(),
        };

    private static async Task<JsonElement> HeartbeatAsync(HttpClient cliente)
    {
        var resposta = await cliente.PostAsJsonAsync("/api/agente/heartbeat", new { hostname = "notebook-teste-bloqueio" });
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        return await resposta.Content.ReadFromJsonAsync<JsonElement>();
    }

    /// <summary>
    /// O bug: o heartbeat que entregava o Bloquear respondia deveEstarBloqueado=false (o status só
    /// muda na confirmação) e o agente Android destravava logo depois de aplicar. Inclui um
    /// Desbloquear ANTIGO para garantir que vale o comando mais recente.
    /// </summary>
    [Fact]
    public async Task Heartbeat_ComBloquearPendenteMaisRecente_MandaFicarBloqueado()
    {
        using var escopo = _factory.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<TrackerDbContext>();
        if (!db.Database.CanConnect()) return; // sem banco disponível (CI): nada a verificar

        var (notebook, token) = await CriarNotebookComAgenteAsync(db, StatusDispositivo.EmEstoque);
        db.ComandosRemotos.Add(Comando(notebook.Id, TipoComandoRemoto.Desbloquear, StatusComandoRemoto.Confirmado,
            DateTime.UtcNow.AddHours(-1)));
        db.ComandosRemotos.Add(Comando(notebook.Id, TipoComandoRemoto.Bloquear, StatusComandoRemoto.Pendente,
            DateTime.UtcNow));
        await db.SaveChangesAsync();

        try
        {
            var cliente = _factory.CreateClient();
            cliente.DefaultRequestHeaders.Add("X-Agent-Token", token);

            var primeiro = await HeartbeatAsync(cliente);
            Assert.True(primeiro.GetProperty("deveEstarBloqueado").GetBoolean());

            // Segundo ciclo: o comando já foi marcado Enviado, e o estado desejado não pode virar.
            var segundo = await HeartbeatAsync(cliente);
            Assert.True(segundo.GetProperty("deveEstarBloqueado").GetBoolean());
        }
        finally
        {
            db.Dispositivos.Remove(notebook); // comandos saem em cascata
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task Heartbeat_ComDesbloquearMaisRecente_MandaDesbloquear()
    {
        using var escopo = _factory.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<TrackerDbContext>();
        if (!db.Database.CanConnect()) return;

        var (notebook, token) = await CriarNotebookComAgenteAsync(db, StatusDispositivo.Bloqueado);
        db.ComandosRemotos.Add(Comando(notebook.Id, TipoComandoRemoto.Bloquear, StatusComandoRemoto.Confirmado,
            DateTime.UtcNow.AddHours(-1)));
        db.ComandosRemotos.Add(Comando(notebook.Id, TipoComandoRemoto.Desbloquear, StatusComandoRemoto.Pendente,
            DateTime.UtcNow));
        await db.SaveChangesAsync();

        try
        {
            var cliente = _factory.CreateClient();
            cliente.DefaultRequestHeaders.Add("X-Agent-Token", token);

            var resposta = await HeartbeatAsync(cliente);
            Assert.False(resposta.GetProperty("deveEstarBloqueado").GetBoolean());
        }
        finally
        {
            db.Dispositivos.Remove(notebook);
            await db.SaveChangesAsync();
        }
    }

    /// <summary>Confirmar o desbloqueio de um aparelho sem detentor não pode deixá-lo "Em uso".</summary>
    [Fact]
    public async Task ConfirmarDesbloqueio_SemDetentor_VaiParaEstoque()
    {
        using var escopo = _factory.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<TrackerDbContext>();
        if (!db.Database.CanConnect()) return;

        var (notebook, token) = await CriarNotebookComAgenteAsync(db, StatusDispositivo.Bloqueado);
        var desbloquear = Comando(notebook.Id, TipoComandoRemoto.Desbloquear, StatusComandoRemoto.Enviado, DateTime.UtcNow);
        db.ComandosRemotos.Add(desbloquear);
        await db.SaveChangesAsync();

        try
        {
            var cliente = _factory.CreateClient();
            cliente.DefaultRequestHeaders.Add("X-Agent-Token", token);

            var resposta = await cliente.PostAsync($"/api/agente/comandos/{desbloquear.Id}/confirmar", null);
            Assert.Equal(HttpStatusCode.NoContent, resposta.StatusCode);

            await db.Entry(notebook).ReloadAsync();
            Assert.Equal(StatusDispositivo.EmEstoque, notebook.Status);
        }
        finally
        {
            db.Dispositivos.Remove(notebook);
            await db.SaveChangesAsync();
        }
    }

    /// <summary>Desbloquear não tira da manutenção: só sai de Bloqueado.</summary>
    [Fact]
    public async Task ConfirmarDesbloqueio_EmManutencao_ContinuaEmManutencao()
    {
        using var escopo = _factory.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<TrackerDbContext>();
        if (!db.Database.CanConnect()) return;

        var (notebook, token) = await CriarNotebookComAgenteAsync(db, StatusDispositivo.EmManutencao);
        var desbloquear = Comando(notebook.Id, TipoComandoRemoto.Desbloquear, StatusComandoRemoto.Enviado, DateTime.UtcNow);
        db.ComandosRemotos.Add(desbloquear);
        await db.SaveChangesAsync();

        try
        {
            var cliente = _factory.CreateClient();
            cliente.DefaultRequestHeaders.Add("X-Agent-Token", token);

            await cliente.PostAsync($"/api/agente/comandos/{desbloquear.Id}/confirmar", null);

            await db.Entry(notebook).ReloadAsync();
            Assert.Equal(StatusDispositivo.EmManutencao, notebook.Status);
        }
        finally
        {
            db.Dispositivos.Remove(notebook);
            await db.SaveChangesAsync();
        }
    }

    private HttpClient ClienteDeEnrollment()
    {
        var cliente = _factory.CreateClient();
        cliente.DefaultRequestHeaders.Add("X-Enrollment-Key", ChaveDeTeste);
        return cliente;
    }

    private static Task<HttpResponseMessage> RegistrarAsync(HttpClient cliente, string numeroSerie, string? assetTag) =>
        cliente.PostAsJsonAsync("/api/agente/auto-enrollment", new
        {
            hostname = "desktop-montado",
            numeroSerie,
            sistemaOperacional = "Windows",
            versaoSO = "10.0",
            macAddress = (string?)null,
            tipo = "Notebook",
            imei = (string?)null,
            assetTag,
        });

    /// <summary>Validação vem antes de qualquer consulta: não precisa de banco.</summary>
    [Fact]
    public async Task AutoEnrollment_SerialGenericoSemPatrimonio_Recusa400()
    {
        var resposta = await RegistrarAsync(ClienteDeEnrollment(), "To be filled by O.E.M.", assetTag: null);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    /// <summary>
    /// O caso que motivou a regra: duas máquinas com o MESMO serial genérico. Com patrimônios
    /// diferentes, viram dois ativos — antes, a segunda reencontrava a primeira e roubava o token.
    /// </summary>
    [Fact]
    public async Task AutoEnrollment_SerialGenericoComPatrimonio_NaoColideEntreMaquinas()
    {
        using var escopo = _factory.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<TrackerDbContext>();
        if (!db.Database.CanConnect()) return;

        var patrimonioA = $"PAT-A-{Guid.NewGuid():N}"[..20];
        var patrimonioB = $"PAT-B-{Guid.NewGuid():N}"[..20];
        var cliente = ClienteDeEnrollment();

        try
        {
            var respostaA = await RegistrarAsync(cliente, "Default string", patrimonioA);
            var respostaB = await RegistrarAsync(cliente, "Default string", patrimonioB);
            Assert.Equal(HttpStatusCode.OK, respostaA.StatusCode);
            Assert.Equal(HttpStatusCode.OK, respostaB.StatusCode);

            var idA = (await respostaA.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("dispositivoId").GetGuid();
            var idB = (await respostaB.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("dispositivoId").GetGuid();
            Assert.NotEqual(idA, idB);

            var a = await db.Dispositivos.AsNoTracking().SingleAsync(d => d.Id == idA);
            // O genérico não é gravado: o serial vira um derivado do patrimônio (único como ele).
            Assert.Equal($"SEM-SERIE-{patrimonioA}", a.NumeroSerie);
        }
        finally
        {
            var criados = await db.Dispositivos.IgnoreQueryFilters()
                .Where(d => d.AssetTag == patrimonioA || d.AssetTag == patrimonioB).ToListAsync();
            db.Dispositivos.RemoveRange(criados);
            await db.SaveChangesAsync();
        }
    }
}
