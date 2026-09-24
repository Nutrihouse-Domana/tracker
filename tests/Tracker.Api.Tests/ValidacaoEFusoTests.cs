using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Tracker.Api.Json;
using Tracker.Application.Common;
using Tracker.Application.Dtos;
using Tracker.Application.Seguranca;
using Tracker.Application.Validators;
using Tracker.Domain.Enums;
using Tracker.Infrastructure.Data;
using Xunit;

namespace Tracker.Api.Tests;

/// <summary>
/// Validadores de entrada, fuso de Brasília, serial genérico de BIOS, backfill de permissões e a
/// configuração de JSON compartilhada — tudo que dá para verificar sem banco.
/// </summary>
public class ValidacaoEFusoTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ValidacaoEFusoTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
    }

    // ---- Enums fora do intervalo (o conversor aceita número no JSON: "status": 99 passaria) ----

    [Fact]
    public void StatusDispositivoForaDoEnum_EhRecusado()
    {
        var validador = new AtualizarStatusDispositivoRequestValidator();

        Assert.False(validador.Validate(new AtualizarStatusDispositivoRequest((StatusDispositivo)99)).IsValid);
        Assert.True(validador.Validate(new AtualizarStatusDispositivoRequest(StatusDispositivo.EmEstoque)).IsValid);
    }

    [Fact]
    public void StatusReparoForaDoEnum_EhRecusado()
    {
        var validador = new AtualizarStatusReparoRequestValidator();

        Assert.False(validador.Validate(new AtualizarStatusReparoRequest((StatusReparo)99)).IsValid);
        Assert.True(validador.Validate(new AtualizarStatusReparoRequest(StatusReparo.Concluido)).IsValid);
    }

    /// <summary>Sem a validação, um tipo desconhecido era gravado e auditado como "Desbloqueou".</summary>
    [Fact]
    public void TipoComandoForaDoEnum_EhRecusado()
    {
        var validador = new EmitirComandoRequestValidator();

        Assert.False(validador.Validate(new EmitirComandoRequest((TipoComandoRemoto)7)).IsValid);
        Assert.True(validador.Validate(new EmitirComandoRequest(TipoComandoRemoto.Bloquear)).IsValid);
    }

    [Fact]
    public void TipoAlertaForaDoEnum_EhRecusado()
    {
        var validador = new AlertaAgenteRequestValidator();

        Assert.False(validador.Validate(new AlertaAgenteRequest((TipoAlertaDispositivo)42, null)).IsValid);
    }

    // ---- Serial genérico de BIOS ----

    [Theory]
    [InlineData("To be filled by O.E.M.")]
    [InlineData("  to be filled by o.e.m.  ")]
    [InlineData("Default string")]
    [InlineData("SYSTEM SERIAL NUMBER")]
    [InlineData("0")]
    [InlineData("000000")]
    [InlineData("N/A")]
    [InlineData("123456789")]
    [InlineData("xxxxxxxxxx")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void SerialGenerico_Reconhecido(string? serial)
    {
        Assert.True(SerialGenerico.EhGenerico(serial));
    }

    [Theory]
    [InlineData("5CD1234XYZ")]
    [InlineData("ANDROID-abc123")]
    [InlineData("R58N123ABC")]
    [InlineData("1000")]
    public void SerialReal_NaoEhGenerico(string serial)
    {
        Assert.False(SerialGenerico.EhGenerico(serial));
    }

    private static AutoEnrollmentRequest Enrollment(string numeroSerie, string? assetTag = null) => new(
        "host", numeroSerie, "Windows", null, null, TipoDispositivo.Notebook, null, AssetTag: assetTag);

    [Fact]
    public void AutoEnrollment_SerialGenericoSemPatrimonio_EhRecusado()
    {
        var validador = new AutoEnrollmentRequestValidator();

        Assert.False(validador.Validate(Enrollment("To be filled by O.E.M.")).IsValid);
        Assert.False(validador.Validate(Enrollment("")).IsValid);
    }

    /// <summary>Com patrimônio o registro passa — o controller descarta o serial genérico.</summary>
    [Fact]
    public void AutoEnrollment_SerialGenericoComPatrimonio_EhAceito()
    {
        var validador = new AutoEnrollmentRequestValidator();

        Assert.True(validador.Validate(Enrollment("Default string", "PAT-0001")).IsValid);
        Assert.True(validador.Validate(Enrollment("5CD1234XYZ")).IsValid);
    }

    // ---- Fuso de Brasília ----

    /// <summary>21h30 de Brasília já é o dia seguinte em UTC: o termo não pode sair com a data de amanhã.</summary>
    [Fact]
    public void ParaBrasilia_ConverteDeUtc()
    {
        var utc = new DateTime(2026, 3, 11, 0, 30, 0, DateTimeKind.Utc);

        var local = FusoBrasil.ParaBrasilia(utc);

        Assert.Equal(new DateTime(2026, 3, 10, 21, 30, 0), local);
    }

    /// <summary>O SQL Server devolve timestamps com Kind=Unspecified: são UTC e assim devem ser lidos.</summary>
    [Fact]
    public void ParaBrasilia_TrataUnspecifiedComoUtc()
    {
        var doBanco = new DateTime(2026, 3, 11, 0, 30, 0, DateTimeKind.Unspecified);

        Assert.Equal(new DateTime(2026, 3, 10, 21, 30, 0), FusoBrasil.ParaBrasilia(doBanco));
    }

    [Fact]
    public void LimitesDoDia_SaoMeiaNoiteDeBrasiliaEmUtc()
    {
        var dia = new DateOnly(2026, 3, 10);

        Assert.Equal(new DateTime(2026, 3, 10, 3, 0, 0), FusoBrasil.InicioDoDiaEmUtc(dia));
        // Fim exclusivo: início do dia seguinte (use com "menor que").
        Assert.Equal(new DateTime(2026, 3, 11, 3, 0, 0), FusoBrasil.FimDoDiaEmUtc(dia));
    }

    // ---- Backfill de permissões do upgrade ----

    [Fact]
    public void Backfill_SoParaAdmSemPermissaoNenhuma()
    {
        Assert.True(DbSeeder.PrecisaDoBackfill([]));
        Assert.True(DbSeeder.PrecisaDoBackfill([new Claim("outra", "x")]));
    }

    /// <summary>Quem teve permissões revogadas (mas não todas) não recebe tudo de volta no start.</summary>
    [Fact]
    public void Backfill_NaoTocaQuemJaTemAlgumaPermissao()
    {
        Assert.False(DbSeeder.PrecisaDoBackfill([new Claim(Permissoes.TipoClaim, Permissoes.DispositivosVer)]));
    }

    /// <summary>Nem quem ficou com ZERO permissões depois de já ter sido configurado (marca presente).</summary>
    [Fact]
    public void Backfill_NaoTocaQuemJaFoiConfigurado()
    {
        Assert.False(DbSeeder.PrecisaDoBackfill([new Claim(DbSeeder.TipoClaimPermissoesConfiguradas, "1")]));
    }

    // ---- JSON: controllers e SignalR com a MESMA configuração ----

    /// <summary>
    /// O hub tem serialização própria; se ele perder o conversor de enum, os agentes descartam o
    /// comando em silêncio. Confere as opções registradas de verdade no contêiner.
    /// </summary>
    [Fact]
    public void ControllersESignalR_UsamJsonConfig()
    {
        using var escopo = _factory.Services.CreateScope();
        var controllers = escopo.ServiceProvider
            .GetRequiredService<IOptions<Microsoft.AspNetCore.Mvc.JsonOptions>>().Value.JsonSerializerOptions;
        var hub = escopo.ServiceProvider
            .GetRequiredService<IOptions<JsonHubProtocolOptions>>().Value.PayloadSerializerOptions;

        foreach (var opcoes in new[] { controllers, hub })
        {
            Assert.Contains(opcoes.Converters, c => c is System.Text.Json.Serialization.JsonStringEnumConverter);
            Assert.Contains(opcoes.Converters, c => c is UtcDateTimeConverter);
        }

        var json = JsonSerializer.Serialize(new ComandoPendenteDto(Guid.Empty, TipoComandoRemoto.Bloquear), hub);
        Assert.Contains("\"Bloquear\"", json);
    }
}
