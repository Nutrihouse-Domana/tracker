using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Tracker.Application.Dtos;
using Tracker.Application.Impressoras;
using Tracker.Application.Seguranca;
using Tracker.Domain.Entities;
using Tracker.Domain.Enums;
using Tracker.Infrastructure.Data;
using Tracker.Infrastructure.Impressoras;
using Xunit;

namespace Tracker.Api.Tests;

/// <summary>
/// Desliga o ciclo SNMP em TODO o projeto de testes: o WebApplicationFactory sobe os hosted
/// services, e sem isto o worker sairia consultando as impressoras cadastradas no banco de dev.
/// Variável de ambiente vence appsettings/user-secrets.
/// </summary>
internal static class DesligaSnmpNosTestes
{
    [ModuleInitializer]
    internal static void Inicializar() => Environment.SetEnvironmentVariable("Snmp__Habilitado", "false");
}

/// <summary>
/// Monitoramento SNMP de impressoras: interpretação das OIDs (pura), fallback v2c→v1, alertas sem
/// duplicar, páginas no período, poda da retenção e endpoints. Os testes com banco pulam sem SQL Server.
/// </summary>
public class MonitoramentoImpressoraTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly ClienteSnmpFake _cliente = new();

    public MonitoramentoImpressoraTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Snmp:Habilitado"] = "false",
                    ["Snmp:FalhasParaOffline"] = "2",
                    ["Snmp:LimiteSuprimentoPercentual"] = "15",
                    ["Snmp:RetencaoDias"] = "730",
                    ["Snmp:TentarV1"] = "true",
                }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IClienteSnmp>();
                services.AddSingleton<IClienteSnmp>(_cliente);

                // Autenticação de teste: um ADM com as permissões do monitoramento, sem cookie.
                services.AddAuthentication(o =>
                    {
                        o.DefaultScheme = AutenticacaoTeste.Esquema;
                        o.DefaultAuthenticateScheme = AutenticacaoTeste.Esquema;
                        o.DefaultChallengeScheme = AutenticacaoTeste.Esquema;
                        o.DefaultForbidScheme = AutenticacaoTeste.Esquema;
                    })
                    .AddScheme<AuthenticationSchemeOptions, AutenticacaoTeste>(AutenticacaoTeste.Esquema, _ => { });
                services.RemoveAll<IClaimsTransformation>();
                services.AddSingleton<IClaimsTransformation, SemTransformacao>();
            });
        });
    }

    // ------------------------------------------------------------------ interpretação (pura)

    [Theory]
    [InlineData(50L, 100L, 50)]
    [InlineData(1L, 3L, 33)]
    [InlineData(0L, 100L, 0)]
    [InlineData(120L, 100L, 100)] // nível acima da capacidade: limita
    [InlineData(-1L, 100L, null)]  // other
    [InlineData(-2L, 100L, null)]  // unknown
    [InlineData(-3L, 100L, null)]  // "tem algum"
    [InlineData(50L, -2L, null)]   // capacidade desconhecida
    [InlineData(50L, 0L, null)]
    public void Percentual_TrataValoresEspeciaisComoNulo(long nivel, long maximo, int? esperado)
    {
        Assert.Equal(esperado, InterpretacaoSnmpImpressora.Percentual(nivel, maximo));
    }

    [Theory]
    [InlineData(2L, 3L, "Ociosa")]
    [InlineData(2L, 4L, "Imprimindo")]
    [InlineData(2L, 5L, "Aquecendo")]
    [InlineData(5L, 3L, "Parada")]
    [InlineData(3L, 3L, "Ociosa, com aviso")]
    [InlineData(2L, null, "Em funcionamento")]
    [InlineData(null, null, null)]
    public void EstadoEmTexto_TraduzHostMib(long? device, long? printer, string? esperado)
    {
        Assert.Equal(esperado, InterpretacaoSnmpImpressora.EstadoEmTexto(device, printer));
    }

    [Fact]
    public void EstadoEmTexto_AnexaBitsDeErro()
    {
        // bit 1 (sem papel) e bit 4 (porta aberta) no 1º byte: 0100 1000
        var estado = InterpretacaoSnmpImpressora.EstadoEmTexto(3, 3, [0x48]);
        Assert.Equal("Ociosa, com aviso — sem papel, porta aberta", estado);
    }

    [Fact]
    public void Interpretar_MontaSuprimentosComCorEContador()
    {
        var escalares = new Dictionary<string, ValorSnmp>
        {
            [OidsImpressora.SysDescr] = new(OidsImpressora.SysDescr, Texto: "HP LaserJet"),
            [OidsImpressora.HrDeviceStatus] = new(OidsImpressora.HrDeviceStatus, Numero: 2),
            [OidsImpressora.HrPrinterStatus] = new(OidsImpressora.HrPrinterStatus, Numero: 3),
            [OidsImpressora.PrtMarkerLifeCount] = new(OidsImpressora.PrtMarkerLifeCount, Numero: 12345),
        };
        var tabelas = new List<ValorSnmp>
        {
            new($"{OidsImpressora.SuprimentoDescricao}.1", Texto: "Black Toner"),
            new($"{OidsImpressora.SuprimentoDescricao}.2", Texto: "Imaging Drum"),
            new($"{OidsImpressora.SuprimentoMaximo}.1", Numero: 1000),
            new($"{OidsImpressora.SuprimentoMaximo}.2", Numero: 100),
            new($"{OidsImpressora.SuprimentoNivel}.1", Numero: 80),
            new($"{OidsImpressora.SuprimentoNivel}.2", Numero: -3),
            new($"{OidsImpressora.SuprimentoColoranteIndice}.1", Numero: 1),
            new($"{OidsImpressora.SuprimentoColoranteIndice}.2", Numero: 0),
            new($"{OidsImpressora.ColoranteValor}.1", Texto: "black"),
        };

        var leitura = InterpretacaoSnmpImpressora.Interpretar(escalares, tabelas);

        Assert.Equal(12345, leitura.ContadorPaginas);
        Assert.Equal("Ociosa", leitura.EstadoReportado);
        Assert.Equal(2, leitura.Suprimentos.Count);
        Assert.Equal(new SuprimentoImpressoraDto("Black Toner", "preto", 8), leitura.Suprimentos[0]);
        Assert.Equal(new SuprimentoImpressoraDto("Imaging Drum", null, null), leitura.Suprimentos[1]);
    }

    [Fact]
    public void Suprimentos_JsonNoFormatoCombinado()
    {
        var json = InterpretacaoSnmpImpressora.SerializarSuprimentos([new("Toner", "preto", 10)]);
        Assert.Equal("[{\"descricao\":\"Toner\",\"cor\":\"preto\",\"percentual\":10}]", json);
        Assert.Empty(InterpretacaoSnmpImpressora.DesserializarSuprimentos("lixo"));
    }

    [Fact]
    public void PaginasNoPeriodo_MaiorMenosMenorValido()
    {
        Assert.Equal(150, InterpretacaoSnmpImpressora.PaginasNoPeriodo([1000, null, 1100, 1150, -1]));
        Assert.Null(InterpretacaoSnmpImpressora.PaginasNoPeriodo([1000, null]));
        Assert.Null(InterpretacaoSnmpImpressora.PaginasNoPeriodo([]));
    }

    [Fact]
    public void DeveAlertarOffline_SoComNFalhasSeguidas()
    {
        Assert.False(InterpretacaoSnmpImpressora.DeveAlertarOffline([false], 2));
        Assert.False(InterpretacaoSnmpImpressora.DeveAlertarOffline([false, true], 2));
        Assert.True(InterpretacaoSnmpImpressora.DeveAlertarOffline([false, false, true], 2));
    }

    // ------------------------------------------------------------------ leitor (fake de rede)

    [Fact]
    public async Task Leitor_SemRespostaEmV2c_TentaV1()
    {
        var cliente = new ClienteSnmpFake();
        cliente.Responder("10.0.0.5", VersaoSnmp.V1, contador: 500);
        var leitor = CriarLeitor(cliente);

        var resultado = await leitor.LerAsync("10.0.0.5", CancellationToken.None);

        Assert.True(resultado.Online);
        Assert.Equal(500, resultado.Dados!.ContadorPaginas);
        Assert.Contains(cliente.Chamadas, c => c.Endereco == "10.0.0.5" && c.Versao == VersaoSnmp.V2c);
    }

    [Fact]
    public async Task Leitor_SemRespostaNenhuma_DevolveOfflineComMotivo()
    {
        var leitor = CriarLeitor(new ClienteSnmpFake());

        var resultado = await leitor.LerAsync("10.0.0.99", CancellationToken.None);

        Assert.False(resultado.Online);
        Assert.Contains("Sem resposta", resultado.Erro);
    }

    /// <summary>
    /// Cliente real contra uma porta UDP fechada no loopback: a exceção da biblioteca/socket tem de
    /// virar FalhaSnmpException "sem resposta" (senão o leitor não tentaria v1 nem marcaria offline).
    /// </summary>
    [Fact]
    public async Task ClienteReal_PortaFechada_ViraFalhaSemResposta()
    {
        var alvo = new AlvoSnmp("127.0.0.1", 39161, "public", 300, VersaoSnmp.V2c);

        var falha = await Assert.ThrowsAsync<FalhaSnmpException>(() =>
            new ClienteSnmpSharpSnmp().GetAsync(alvo, OidsImpressora.Escalares, CancellationToken.None));

        Assert.True(falha.SemResposta);
    }

    private static LeitorSnmpImpressora CriarLeitor(IClienteSnmp cliente) =>
        new(cliente, new OpcoesFixas(new OpcoesSnmp()), NullLogger<LeitorSnmpImpressora>.Instance);

    // ------------------------------------------------------------------ serviço (banco)

    private async Task<Impressora> CriarImpressoraAsync(
        TrackerDbContext db, ConexaoImpressora conexao = ConexaoImpressora.Rede, string? endereco = null, bool monitorar = true)
    {
        var impressora = new Impressora
        {
            NumeroSerie = $"IMP-TESTE-{Guid.NewGuid():N}",
            DataAquisicao = DateTime.UtcNow,
            Conexao = conexao,
            EnderecoRede = endereco ?? $"imp-{Guid.NewGuid():N}.teste",
            MonitorarSnmp = monitorar,
        };
        db.Dispositivos.Add(impressora);
        await db.SaveChangesAsync();
        return impressora;
    }

    private static async Task RemoverAsync(TrackerDbContext db, Guid id)
    {
        db.ChangeTracker.Clear();
        await db.Dispositivos.IgnoreQueryFilters().Where(d => d.Id == id).ExecuteDeleteAsync(); // leituras/alertas em cascata
    }

    private static ResultadoLeituraSnmp Offline() => new(false, null, "Sem resposta (teste).");

    private static ResultadoLeituraSnmp Online(long contador, params SuprimentoImpressoraDto[] suprimentos) =>
        new(true, new LeituraInterpretada("Teste", contador, "Ociosa", suprimentos), null);

    [Fact]
    public async Task AlertaOffline_SoAposNFalhas_ESemDuplicar()
    {
        using var escopo = _factory.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<TrackerDbContext>();
        if (!db.Database.CanConnect()) return;
        var servico = escopo.ServiceProvider.GetRequiredService<MonitoramentoImpressorasService>();
        var impressora = await CriarImpressoraAsync(db);
        try
        {
            var t0 = DateTime.UtcNow;
            await servico.RegistrarAsync(impressora, Offline(), t0, CancellationToken.None);
            Assert.Equal(0, await ContarAlertasAsync(db, impressora.Id, TipoAlertaDispositivo.ImpressoraOffline));

            await servico.RegistrarAsync(impressora, Offline(), t0.AddMinutes(15), CancellationToken.None);
            Assert.Equal(1, await ContarAlertasAsync(db, impressora.Id, TipoAlertaDispositivo.ImpressoraOffline));

            // Terceira falha com o alerta ainda não reconhecido: não cria outro.
            await servico.RegistrarAsync(impressora, Offline(), t0.AddMinutes(30), CancellationToken.None);
            Assert.Equal(1, await ContarAlertasAsync(db, impressora.Id, TipoAlertaDispositivo.ImpressoraOffline));

            Assert.False(impressora.Online);
            Assert.Equal(3, await db.LeiturasImpressora.CountAsync(l => l.ImpressoraId == impressora.Id));
        }
        finally
        {
            await RemoverAsync(db, impressora.Id);
        }
    }

    [Fact]
    public async Task SuprimentoBaixo_AlertaComDetalhe_ESemDuplicar()
    {
        using var escopo = _factory.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<TrackerDbContext>();
        if (!db.Database.CanConnect()) return;
        var servico = escopo.ServiceProvider.GetRequiredService<MonitoramentoImpressorasService>();
        var impressora = await CriarImpressoraAsync(db);
        try
        {
            var t0 = DateTime.UtcNow;
            await servico.RegistrarAsync(impressora, Online(100, new SuprimentoImpressoraDto("Toner preto", "preto", 40)), t0, CancellationToken.None);
            Assert.Equal(0, await ContarAlertasAsync(db, impressora.Id, TipoAlertaDispositivo.SuprimentoBaixo));

            await servico.RegistrarAsync(impressora, Online(110, new SuprimentoImpressoraDto("Toner preto", "preto", 15)), t0.AddMinutes(15), CancellationToken.None);
            await servico.RegistrarAsync(impressora, Online(120, new SuprimentoImpressoraDto("Toner preto", "preto", 9)), t0.AddMinutes(30), CancellationToken.None);

            var alertas = await db.AlertasDispositivo.AsNoTracking()
                .Where(a => a.DispositivoId == impressora.Id && a.Tipo == TipoAlertaDispositivo.SuprimentoBaixo)
                .ToListAsync();
            Assert.Single(alertas);
            Assert.Equal("Toner preto: 15%", alertas[0].Detalhe);
            Assert.Equal(120, impressora.ContadorPaginas);
            Assert.True(impressora.Online);
        }
        finally
        {
            await RemoverAsync(db, impressora.Id);
        }
    }

    [Fact]
    public async Task Leituras_CalculaPaginasNoPeriodoPeloFusoDeBrasilia()
    {
        using var escopo = _factory.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<TrackerDbContext>();
        if (!db.Database.CanConnect()) return;
        var servico = escopo.ServiceProvider.GetRequiredService<MonitoramentoImpressorasService>();
        var impressora = await CriarImpressoraAsync(db);
        try
        {
            // 10/03/2026 em Brasília vai de 10/03 03:00Z até 11/03 03:00Z (exclusivo).
            db.LeiturasImpressora.AddRange(
                Leitura(impressora.Id, new DateTime(2026, 3, 10, 2, 59, 0, DateTimeKind.Utc), 900),  // 09/03 em Brasília
                Leitura(impressora.Id, new DateTime(2026, 3, 10, 3, 0, 0, DateTimeKind.Utc), 1000),
                Leitura(impressora.Id, new DateTime(2026, 3, 10, 15, 0, 0, DateTimeKind.Utc), null, online: false),
                Leitura(impressora.Id, new DateTime(2026, 3, 11, 2, 59, 0, DateTimeKind.Utc), 1250),
                Leitura(impressora.Id, new DateTime(2026, 3, 11, 3, 0, 0, DateTimeKind.Utc), 1300)); // 11/03
            await db.SaveChangesAsync();

            var dia = new DateOnly(2026, 3, 10);
            var resultado = await servico.ObterLeiturasAsync(impressora.Id, dia, dia, CancellationToken.None);

            Assert.Equal(SituacaoConsultaImpressora.Ok, resultado.Situacao);
            Assert.Equal(3, resultado.Valor!.TotalLeituras);
            Assert.Equal(250, resultado.Valor.PaginasNoPeriodo);
            Assert.Equal(1000, resultado.Valor.ContadorInicial);
            Assert.Equal(1250, resultado.Valor.ContadorFinal);

            var invertido = await servico.ObterLeiturasAsync(impressora.Id, dia, dia.AddDays(-1), CancellationToken.None);
            Assert.Equal(SituacaoConsultaImpressora.PeriodoInvalido, invertido.Situacao);
        }
        finally
        {
            await RemoverAsync(db, impressora.Id);
        }
    }

    [Fact]
    public async Task Poda_ApagaSoOQuePassouDaRetencao()
    {
        using var escopo = _factory.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<TrackerDbContext>();
        if (!db.Database.CanConnect()) return;
        var servico = escopo.ServiceProvider.GetRequiredService<MonitoramentoImpressorasService>();
        var impressora = await CriarImpressoraAsync(db);
        try
        {
            var agora = DateTime.UtcNow;
            var velha = Leitura(impressora.Id, agora.AddDays(-731), 10);
            var recente = Leitura(impressora.Id, agora.AddDays(-729), 20);
            db.LeiturasImpressora.AddRange(velha, recente);
            await db.SaveChangesAsync();

            await servico.PodarLeiturasAsync(agora, CancellationToken.None);

            var restantes = await db.LeiturasImpressora.AsNoTracking()
                .Where(l => l.ImpressoraId == impressora.Id).Select(l => l.Id).ToListAsync();
            Assert.Equal([recente.Id], restantes);
        }
        finally
        {
            await RemoverAsync(db, impressora.Id);
        }
    }

    [Fact]
    public async Task ImpressoraUsb_NaoEntraNoCiclo_ENaoEhConsultada()
    {
        using var escopo = _factory.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<TrackerDbContext>();
        if (!db.Database.CanConnect()) return;
        var servico = escopo.ServiceProvider.GetRequiredService<MonitoramentoImpressorasService>();
        var usb = await CriarImpressoraAsync(db, ConexaoImpressora.Usb, endereco: "usb-nao-consultar.teste");
        var rede = await CriarImpressoraAsync(db);
        try
        {
            var ids = await servico.ListarMonitoraveisAsync(CancellationToken.None);
            Assert.DoesNotContain(usb.Id, ids);
            Assert.Contains(rede.Id, ids);

            var resultado = await servico.LerERegistrarAsync(usb.Id, CancellationToken.None);
            Assert.Equal(SituacaoConsultaImpressora.NaoMonitoravel, resultado.Situacao);
            Assert.DoesNotContain(_cliente.Chamadas, c => c.Endereco == "usb-nao-consultar.teste");
        }
        finally
        {
            await RemoverAsync(db, usb.Id);
            await RemoverAsync(db, rede.Id);
        }
    }

    // ------------------------------------------------------------------ endpoints

    [Fact]
    public async Task Endpoints_404ParaInexistenteOuNaoImpressora_400ParaNaoMonitoravel()
    {
        using var escopo = _factory.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<TrackerDbContext>();
        if (!db.Database.CanConnect()) return;

        var notebook = new Notebook
        {
            NumeroSerie = $"IMP-TESTE-NB-{Guid.NewGuid():N}",
            AssetTag = $"IMP-TESTE-NB-{Guid.NewGuid():N}",
            Hostname = "nb-teste-impressora",
            SistemaOperacional = "Windows",
            DataAquisicao = DateTime.UtcNow,
        };
        db.Dispositivos.Add(notebook);
        await db.SaveChangesAsync();
        var usb = await CriarImpressoraAsync(db, ConexaoImpressora.Usb);
        var rede = await CriarImpressoraAsync(db, endereco: "10.123.45.67");
        _cliente.Responder("10.123.45.67", VersaoSnmp.V2c, contador: 4321);

        try
        {
            var http = _factory.CreateClient();

            Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync($"/api/impressoras/{Guid.NewGuid()}/monitoramento")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync($"/api/impressoras/{notebook.Id}/monitoramento")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync($"/api/impressoras/{notebook.Id}/leituras")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await http.PostAsync($"/api/impressoras/{notebook.Id}/ler-agora", null)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await http.PostAsync($"/api/impressoras/{usb.Id}/ler-agora", null)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest,
                (await http.GetAsync($"/api/impressoras/{rede.Id}/leituras?de=2026-03-10&ate=2026-03-01")).StatusCode);

            var lerAgora = await http.PostAsync($"/api/impressoras/{rede.Id}/ler-agora", null);
            Assert.Equal(HttpStatusCode.OK, lerAgora.StatusCode);
            var json = await lerAgora.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(json.GetProperty("online").GetBoolean());
            Assert.Equal(4321, json.GetProperty("contadorPaginas").GetInt64());
            Assert.Equal("Rede", json.GetProperty("conexao").GetString());

            var leituras = await http.GetFromJsonAsync<JsonElement>($"/api/impressoras/{rede.Id}/leituras");
            Assert.Equal(1, leituras.GetProperty("totalLeituras").GetInt32());
        }
        finally
        {
            await RemoverAsync(db, notebook.Id);
            await RemoverAsync(db, usb.Id);
            await RemoverAsync(db, rede.Id);
        }
    }

    // ------------------------------------------------------------------ apoio

    private static LeituraImpressora Leitura(Guid impressoraId, DateTime em, long? contador, bool online = true) =>
        new() { ImpressoraId = impressoraId, CriadoEm = em, Online = online, ContadorPaginas = contador };

    private static Task<int> ContarAlertasAsync(TrackerDbContext db, Guid id, TipoAlertaDispositivo tipo) =>
        db.AlertasDispositivo.CountAsync(a => a.DispositivoId == id && a.Tipo == tipo);

    /// <summary>Fake de rede: responde só aos endereços/versões configurados; o resto dá timeout.</summary>
    private sealed class ClienteSnmpFake : IClienteSnmp
    {
        private readonly ConcurrentDictionary<(string, VersaoSnmp), long> _respostas = new();

        public ConcurrentBag<AlvoSnmp> Chamadas { get; } = new();

        public void Responder(string endereco, VersaoSnmp versao, long contador) => _respostas[(endereco, versao)] = contador;

        public Task<IReadOnlyDictionary<string, ValorSnmp>> GetAsync(AlvoSnmp alvo, IReadOnlyList<string> oids, CancellationToken ct)
        {
            Chamadas.Add(alvo);
            if (!_respostas.TryGetValue((alvo.Endereco, alvo.Versao), out var contador))
                throw new FalhaSnmpException("timeout (fake)", semResposta: true);

            IReadOnlyDictionary<string, ValorSnmp> resultado = oids.ToDictionary(o => o, o => o switch
            {
                OidsImpressora.PrtMarkerLifeCount => new ValorSnmp(o, Numero: contador),
                OidsImpressora.HrPrinterStatus => new ValorSnmp(o, Numero: 3),
                _ => new ValorSnmp(o),
            });
            return Task.FromResult(resultado);
        }

        public Task<IReadOnlyList<ValorSnmp>> WalkAsync(AlvoSnmp alvo, string raiz, CancellationToken ct)
        {
            Chamadas.Add(alvo);
            return Task.FromResult<IReadOnlyList<ValorSnmp>>([]);
        }
    }

    private sealed class OpcoesFixas(OpcoesSnmp valor) : IOptionsMonitor<OpcoesSnmp>
    {
        public OpcoesSnmp CurrentValue => valor;
        public OpcoesSnmp Get(string? name) => valor;
        public IDisposable? OnChange(Action<OpcoesSnmp, string?> listener) => null;
    }

    /// <summary>ADM fictício com as permissões do monitoramento.</summary>
    private sealed class AutenticacaoTeste(
        IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string Esquema = "Teste";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identidade = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim("nome", "ADM de teste"),
                new Claim(Permissoes.TipoClaim, Permissoes.DispositivosVer),
                new Claim(Permissoes.TipoClaim, Permissoes.DispositivosCadastrar),
            ], Esquema);
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identidade), Esquema)));
        }
    }

    /// <summary>Não recarrega permissões do banco (o ADM de teste não existe lá).</summary>
    private sealed class SemTransformacao : IClaimsTransformation
    {
        public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal) => Task.FromResult(principal);
    }
}
