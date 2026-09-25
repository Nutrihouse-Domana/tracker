using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tracker.Api.Controllers;
using Tracker.Application.Dispositivos;
using Tracker.Application.Dtos;
using Tracker.Application.Relatorios;
using Tracker.Application.Validators;
using Tracker.Domain.Entities;
using Tracker.Domain.Enums;
using Tracker.Infrastructure.Data;
using Tracker.Infrastructure.Relatorios;
using Xunit;

namespace Tracker.Api.Tests;

/// <summary>
/// Protege o cadastro de impressoras (Fase 1): tipo sem agente, sempre do local, aluguel por
/// interruptor (não pelo prefixo "MAQ") e regra USB x SNMP. Os testes com banco pulam quando não
/// há SQL Server disponível, como os demais.
/// </summary>
public class ImpressoraCadastroTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ImpressoraCadastroTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
    }

    // ---------------------------------------------------------------- regras puras / validação

    [Fact]
    public void Impressora_NasceDoLocal_ComDiscriminadorProprio()
    {
        var impressora = new Impressora();

        Assert.Equal(TipoDispositivo.Impressora, impressora.Tipo);
        Assert.False(impressora.PertenceAUsuario);
        Assert.False(impressora.Alugada);
        Assert.False(impressora.MonitorarSnmp);
        Assert.False(RegrasDispositivo.AceitaDetentor(impressora));
        Assert.True(RegrasDispositivo.AceitaDetentor(new Notebook()));
        Assert.True(RegrasDispositivo.AceitaDetentor(new Periferico()));
    }

    /// <summary>
    /// A regra de aluguel é por tipo: impressora segue o interruptor, os demais o prefixo. Espelho
    /// no site: features/dispositivos/aluguel.ts.
    /// </summary>
    [Theory]
    [InlineData(TipoDispositivo.Impressora, true, "IMP-RH-01", true)]   // sem MAQ, interruptor ligado
    [InlineData(TipoDispositivo.Impressora, false, "MAQ-IMP-01", false)] // MAQ não vale para impressora
    [InlineData(TipoDispositivo.Impressora, null, "MAQ-IMP-01", false)]
    [InlineData(TipoDispositivo.Notebook, null, "MAQ-0142", true)]     // notebook continua pelo prefixo
    [InlineData(TipoDispositivo.Notebook, true, "NHD-0031", false)]    // interruptor não vale fora da impressora
    [InlineData(TipoDispositivo.Mobile, null, "maq-tab-3", true)]
    [InlineData(TipoDispositivo.Periferico, null, "SN-123", false)]
    public void EhAlugado_ImpressoraPeloInterruptor_DemaisPeloPrefixo(
        TipoDispositivo tipo, bool? alugada, string identificacao, bool esperado)
    {
        Assert.Equal(esperado, ClassificacaoAluguel.EhAlugado(tipo, alugada, identificacao));
    }

    private static CriarImpressoraRequest Requisicao(
        ConexaoImpressora conexao = ConexaoImpressora.Rede,
        string? endereco = "192.168.0.50",
        bool monitorar = false,
        string? mac = null,
        Guid? unidadeId = null,
        TipoImpressora tipo = TipoImpressora.Laser,
        string numeroSerie = "IMP-SN-1",
        bool alugada = false) =>
        new(numeroSerie, null, tipo, "HP", "LaserJet M428", false, alugada, conexao, endereco, mac,
            monitorar, DateTime.UtcNow, null, unidadeId ?? Guid.NewGuid());

    [Fact]
    public void Validador_UsbComSnmp_Recusa()
    {
        var resultado = new CriarImpressoraRequestValidator()
            .Validate(Requisicao(ConexaoImpressora.Usb, endereco: null, monitorar: true));

        Assert.False(resultado.IsValid);
        Assert.Contains(resultado.Errors, e => e.ErrorMessage.Contains("USB"));
    }

    [Fact]
    public void Validador_UsbSemEndereco_Aceita()
    {
        var resultado = new CriarImpressoraRequestValidator()
            .Validate(Requisicao(ConexaoImpressora.Usb, endereco: null));

        Assert.True(resultado.IsValid, string.Join("; ", resultado.Errors));
    }

    [Fact]
    public void Validador_SnmpSemEndereco_Recusa()
    {
        var resultado = new DefinirConexaoImpressoraRequestValidator()
            .Validate(new DefinirConexaoImpressoraRequest(ConexaoImpressora.Rede, "  ", null, true));

        Assert.False(resultado.IsValid);
    }

    [Fact]
    public void Validador_RedeComSnmpEEnderecoValido_Aceita()
    {
        var resultado = new DefinirConexaoImpressoraRequestValidator()
            .Validate(new DefinirConexaoImpressoraRequest(
                ConexaoImpressora.Rede, "impressora-rh.nutrihouse.local", "AA:BB:CC:DD:EE:FF", true));

        Assert.True(resultado.IsValid, string.Join("; ", resultado.Errors));
    }

    [Fact]
    public void Validador_EnumsForaDoIntervalo_Recusa()
    {
        var resultado = new CriarImpressoraRequestValidator()
            .Validate(Requisicao((ConexaoImpressora)7, tipo: (TipoImpressora)99));

        Assert.False(resultado.IsValid);
        Assert.Contains(resultado.Errors, e => e.PropertyName == nameof(CriarImpressoraRequest.Conexao));
        Assert.Contains(resultado.Errors, e => e.PropertyName == nameof(CriarImpressoraRequest.TipoImpressora));
    }

    [Fact]
    public void Validador_SemUnidade_Recusa()
    {
        var request = Requisicao() with { UnidadeId = null };

        Assert.False(new CriarImpressoraRequestValidator().Validate(request).IsValid);
    }

    [Theory]
    [InlineData("192.168.0.50", true)]
    [InlineData("10.0.0.1", true)]
    [InlineData("impressora-rh", true)]
    [InlineData("imp01.nutrihouse.local", true)]
    [InlineData("fe80::1", true)]
    [InlineData("192.168.0", false)]     // parece IP, faltou octeto
    [InlineData("300.1.1.1", false)]
    [InlineData("1", false)]             // IPAddress.TryParse aceitaria como 0.0.0.1
    [InlineData("com espaço", false)]
    [InlineData("http://192.168.0.5", false)]
    public void FormatoRede_Endereco(string endereco, bool esperado)
    {
        Assert.Equal(esperado, FormatoRede.EnderecoValido(endereco));
    }

    [Theory]
    [InlineData("AA:BB:CC:DD:EE:FF", true)]
    [InlineData("aa-bb-cc-dd-ee-ff", true)]
    [InlineData("AABBCCDDEEFF", true)]
    [InlineData("AA:BB-CC:DD:EE:FF", false)] // separadores misturados
    [InlineData("AA:BB:CC:DD:EE", false)]
    public void FormatoRede_Mac(string mac, bool esperado)
    {
        Assert.Equal(esperado, FormatoRede.MacValido(mac));
    }

    /// <summary>Segunda barreira: mesmo sem validador, USB nunca fica com endereço nem SNMP ligado.</summary>
    [Fact]
    public void AplicarConexao_Usb_ForcaSemEnderecoESemSnmp()
    {
        var impressora = new Impressora { EnderecoRede = "192.168.0.9", MonitorarSnmp = true };

        RegrasDispositivo.AplicarConexao(impressora, ConexaoImpressora.Usb, "192.168.0.9", " ", true);

        Assert.Equal(ConexaoImpressora.Usb, impressora.Conexao);
        Assert.Null(impressora.EnderecoRede);
        Assert.Null(impressora.MacAddress);
        Assert.False(impressora.MonitorarSnmp);
    }

    [Fact]
    public void AplicarConexao_RedeSemEndereco_DesligaSnmp()
    {
        var impressora = new Impressora();

        RegrasDispositivo.AplicarConexao(impressora, ConexaoImpressora.Rede, "   ", null, true);

        Assert.Null(impressora.EnderecoRede);
        Assert.False(impressora.MonitorarSnmp);

        RegrasDispositivo.AplicarConexao(impressora, ConexaoImpressora.Rede, " 10.0.0.7 ", null, true);
        Assert.Equal("10.0.0.7", impressora.EnderecoRede);
        Assert.True(impressora.MonitorarSnmp);
    }

    // ---------------------------------------------------------------- modelo TPH

    /// <summary>
    /// Fabricante/Modelo/MacAddress são COMPARTILHADOS pela impressora e pelos tipos com agente. Se
    /// o nome da coluna divergir, o EF cria Notebook_Fabricante etc. e os dados de produção somem
    /// da tela (moveriam para colunas novas vazias).
    /// </summary>
    [Theory]
    [InlineData(nameof(Impressora.Fabricante))]
    [InlineData(nameof(Impressora.Modelo))]
    [InlineData(nameof(Impressora.MacAddress))]
    public void Tph_ColunasCompartilhadasComOsTiposComAgente(string propriedade)
    {
        using var escopo = _factory.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<TrackerDbContext>();

        foreach (var tipo in new[] { typeof(Impressora), typeof(Notebook), typeof(Mobile) })
        {
            var prop = db.Model.FindEntityType(tipo)!.FindProperty(propriedade)!;
            Assert.Equal(propriedade, prop.GetColumnName());
        }
    }

    // ---------------------------------------------------------------- com banco

    /// <summary>Controller com um ADM falso logado (a auditoria lê o autor do HttpContext).</summary>
    private static DispositivosController CriarController(IServiceProvider sp)
    {
        var usuario = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim("nome", "ADM de teste das impressoras"),
        }, "Teste"));

        var http = new DefaultHttpContext { User = usuario, RequestServices = sp };
        sp.GetRequiredService<IHttpContextAccessor>().HttpContext = http;

        var controller = ActivatorUtilities.CreateInstance<DispositivosController>(sp);
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return controller;
    }

    private static string? Mensagem(IActionResult resultado)
    {
        var valor = Assert.IsType<BadRequestObjectResult>(resultado).Value;
        return valor?.GetType().GetProperty("message")?.GetValue(valor) as string;
    }

    private static async Task<Unidade> CriarUnidadeAsync(TrackerDbContext db)
    {
        var unidade = new Unidade { Nome = $"Unidade teste impressora {Guid.NewGuid():N}" };
        db.Unidades.Add(unidade);
        await db.SaveChangesAsync();
        return unidade;
    }

    private static async Task LimparAsync(TrackerDbContext db, Unidade unidade, params Guid[] dispositivos)
    {
        db.ChangeTracker.Clear();
        await db.Dispositivos.IgnoreQueryFilters().Where(d => dispositivos.Contains(d.Id)).ExecuteDeleteAsync();
        await db.LogsAuditoria.Where(l => l.EntidadeId != null && dispositivos.Contains(l.EntidadeId.Value))
            .ExecuteDeleteAsync();
        await db.Unidades.Where(u => u.Id == unidade.Id).ExecuteDeleteAsync();
    }

    [Fact]
    public async Task CriarImpressora_GravaTipoLocalEConexaoNormalizada()
    {
        using var escopo = _factory.Services.CreateScope();
        var sp = escopo.ServiceProvider;
        var db = sp.GetRequiredService<TrackerDbContext>();
        if (!db.Database.CanConnect()) return;

        var unidade = await CriarUnidadeAsync(db);
        var id = Guid.Empty;
        try
        {
            var controller = CriarController(sp);
            var resposta = await controller.CriarImpressora(Requisicao(
                ConexaoImpressora.Rede, endereco: " 192.168.0.77 ", monitorar: true,
                unidadeId: unidade.Id, numeroSerie: $"IMP-{Guid.NewGuid():N}", alugada: true));

            var criado = Assert.IsType<CreatedAtActionResult>(resposta.Result);
            var dto = Assert.IsType<DispositivoDto>(criado.Value);
            id = dto.Id;

            Assert.Equal(TipoDispositivo.Impressora, dto.Tipo);
            Assert.False(dto.PertenceAUsuario);
            Assert.Equal(unidade.Id, dto.UnidadeId);
            Assert.True(dto.Alugada);
            Assert.Equal("192.168.0.77", dto.EnderecoRede);
            Assert.True(dto.MonitorarSnmp);
            Assert.Equal("HP", dto.Fabricante);

            db.ChangeTracker.Clear();
            var salva = await db.Set<Impressora>().AsNoTracking().SingleAsync(i => i.Id == id);
            Assert.Equal(StatusDispositivo.EmEstoque, salva.Status);
            Assert.Null(salva.AssetTag); // patrimônio em branco vira nulo (índice único filtrado)
            Assert.Equal("LaserJet M428", salva.Modelo);
        }
        finally
        {
            await LimparAsync(db, unidade, id);
        }
    }

    /// <summary>A impressora não pode quebrar os outros tipos que dividem as colunas do TPH.</summary>
    [Fact]
    public async Task Tph_NotebookEMobileContinuamComFabricanteEModelo()
    {
        using var escopo = _factory.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<TrackerDbContext>();
        if (!db.Database.CanConnect()) return;

        var unidade = await CriarUnidadeAsync(db);
        var notebook = new Notebook
        {
            NumeroSerie = $"NB-IMP-{Guid.NewGuid():N}", AssetTag = $"PAT-{Guid.NewGuid():N}",
            Hostname = "nb-teste", SistemaOperacional = "Windows", DataAquisicao = DateTime.UtcNow,
            Fabricante = "Dell", Modelo = "Latitude 5440", MacAddress = "11:22:33:44:55:66",
        };
        var mobile = new Mobile
        {
            NumeroSerie = $"MB-IMP-{Guid.NewGuid():N}", AssetTag = $"PAT-{Guid.NewGuid():N}",
            Hostname = "tab-teste", SistemaOperacional = "Android", DataAquisicao = DateTime.UtcNow,
            Fabricante = "Samsung", Modelo = "SM-X200",
        };
        var impressora = new Impressora
        {
            NumeroSerie = $"IMP-{Guid.NewGuid():N}", UnidadeId = unidade.Id, DataAquisicao = DateTime.UtcNow,
            Fabricante = "Brother", Modelo = "HL-L5100", MacAddress = "AA:BB:CC:DD:EE:FF",
        };
        db.Dispositivos.AddRange(notebook, mobile, impressora);
        await db.SaveChangesAsync();

        try
        {
            db.ChangeTracker.Clear();
            var todos = await db.Dispositivos.AsNoTracking()
                .Where(d => d.Id == notebook.Id || d.Id == mobile.Id || d.Id == impressora.Id)
                .ToListAsync();

            var nb = Assert.IsType<Notebook>(todos.Single(d => d.Id == notebook.Id));
            Assert.Equal(("Dell", "Latitude 5440", "11:22:33:44:55:66"), (nb.Fabricante, nb.Modelo, nb.MacAddress));
            var mb = Assert.IsType<Mobile>(todos.Single(d => d.Id == mobile.Id));
            Assert.Equal(("Samsung", "SM-X200"), (mb.Fabricante, mb.Modelo));
            var imp = Assert.IsType<Impressora>(todos.Single(d => d.Id == impressora.Id));
            Assert.Equal(("Brother", "HL-L5100", "AA:BB:CC:DD:EE:FF"), (imp.Fabricante, imp.Modelo, imp.MacAddress));
        }
        finally
        {
            await LimparAsync(db, unidade, notebook.Id, mobile.Id, impressora.Id);
        }
    }

    [Fact]
    public async Task Aluguel_AlternaEAudita_EClassificaNoRelatorio()
    {
        using var escopo = _factory.Services.CreateScope();
        var sp = escopo.ServiceProvider;
        var db = sp.GetRequiredService<TrackerDbContext>();
        if (!db.Database.CanConnect()) return;

        var unidade = await CriarUnidadeAsync(db);
        // Impressora SEM "MAQ" e notebook COM "MAQ": os dois devem sair alugados no relatório.
        var impressora = new Impressora
        {
            NumeroSerie = $"IMP-{Guid.NewGuid():N}", IdentificacaoInterna = "IMP-FINANCEIRO",
            UnidadeId = unidade.Id, DataAquisicao = DateTime.UtcNow,
        };
        var notebook = new Notebook
        {
            NumeroSerie = $"NB-{Guid.NewGuid():N}", AssetTag = $"PAT-{Guid.NewGuid():N}",
            IdentificacaoInterna = $"MAQ-{Guid.NewGuid():N}", Hostname = "nb", SistemaOperacional = "Windows",
            DataAquisicao = DateTime.UtcNow,
        };
        db.Dispositivos.AddRange(impressora, notebook);
        await db.SaveChangesAsync();

        try
        {
            var controller = CriarController(sp);
            Assert.IsType<NoContentResult>(
                await controller.DefinirAluguel(impressora.Id, new DefinirAluguelImpressoraRequest(true)));

            db.ChangeTracker.Clear();
            Assert.True((await db.Set<Impressora>().AsNoTracking().SingleAsync(i => i.Id == impressora.Id)).Alugada);

            var log = await db.LogsAuditoria.AsNoTracking()
                .SingleAsync(l => l.EntidadeId == impressora.Id && l.Acao == AcaoAuditoria.DispositivoAluguelDefinido);
            Assert.Equal("ADM de teste das impressoras", log.AdmNome);

            var linhas = await sp.GetRequiredService<IRelatorioInventarioService>().InventarioAsync(somenteAlugados: true);
            Assert.Contains(linhas, l => l.NumeroSerie == impressora.NumeroSerie && l.Tipo == "Impressora");
            Assert.Contains(linhas, l => l.NumeroSerie == notebook.NumeroSerie);

            // O interruptor só existe na impressora: no notebook o alugado é o prefixo.
            var recusa = await controller.DefinirAluguel(notebook.Id, new DefinirAluguelImpressoraRequest(false));
            Assert.Contains("MAQ", Mensagem(recusa));
        }
        finally
        {
            await LimparAsync(db, unidade, impressora.Id, notebook.Id);
        }
    }

    [Fact]
    public async Task Impressora_RecusaAtribuirEPertencerAUsuario()
    {
        using var escopo = _factory.Services.CreateScope();
        var sp = escopo.ServiceProvider;
        var db = sp.GetRequiredService<TrackerDbContext>();
        if (!db.Database.CanConnect()) return;

        var unidade = await CriarUnidadeAsync(db);
        var impressora = new Impressora
        {
            NumeroSerie = $"IMP-{Guid.NewGuid():N}", UnidadeId = unidade.Id, DataAquisicao = DateTime.UtcNow,
        };
        db.Dispositivos.Add(impressora);
        await db.SaveChangesAsync();

        try
        {
            var controller = CriarController(sp);

            var atribuir = await controller.Atribuir(impressora.Id, new AtribuirDispositivoRequest(Guid.NewGuid(), null, null));
            Assert.Equal(RegrasDispositivo.MensagemImpressoraSemDetentor, Mensagem(atribuir.Result!));

            var posse = await controller.DefinirPosse(impressora.Id,
                new DefinirPosseDispositivoRequest(true, null, "qualquer-senha", null));
            Assert.Equal(RegrasDispositivo.MensagemImpressoraSemDetentor, Mensagem(posse));

            // "Em uso" vale como no ativo de local: com unidade definida, entra.
            Assert.IsType<NoContentResult>(await controller.AtualizarStatus(impressora.Id,
                new AtualizarStatusDispositivoRequest(StatusDispositivo.EmUso)));

            db.ChangeTracker.Clear();
            var salva = await db.Set<Impressora>().AsNoTracking().SingleAsync(i => i.Id == impressora.Id);
            Assert.False(salva.PertenceAUsuario);
            Assert.Null(salva.UsuarioAtualId);
            Assert.Equal(StatusDispositivo.EmUso, salva.Status);
        }
        finally
        {
            await LimparAsync(db, unidade, impressora.Id);
        }
    }

    [Fact]
    public async Task Conexao_UsbDesligaSnmpEAudita()
    {
        using var escopo = _factory.Services.CreateScope();
        var sp = escopo.ServiceProvider;
        var db = sp.GetRequiredService<TrackerDbContext>();
        if (!db.Database.CanConnect()) return;

        var unidade = await CriarUnidadeAsync(db);
        var impressora = new Impressora
        {
            NumeroSerie = $"IMP-{Guid.NewGuid():N}", UnidadeId = unidade.Id, DataAquisicao = DateTime.UtcNow,
            Conexao = ConexaoImpressora.Rede, EnderecoRede = "192.168.0.60", MonitorarSnmp = true,
        };
        db.Dispositivos.Add(impressora);
        await db.SaveChangesAsync();

        try
        {
            var controller = CriarController(sp);

            // Validador: USB com SNMP é recusado (400 de validação), nada muda.
            var recusa = await controller.DefinirConexaoImpressora(impressora.Id,
                new DefinirConexaoImpressoraRequest(ConexaoImpressora.Usb, null, null, true));
            Assert.IsNotType<NoContentResult>(recusa);

            Assert.IsType<NoContentResult>(await controller.DefinirConexaoImpressora(impressora.Id,
                new DefinirConexaoImpressoraRequest(ConexaoImpressora.Usb, "192.168.0.60", null, false)));

            db.ChangeTracker.Clear();
            var salva = await db.Set<Impressora>().AsNoTracking().SingleAsync(i => i.Id == impressora.Id);
            Assert.Equal(ConexaoImpressora.Usb, salva.Conexao);
            Assert.Null(salva.EnderecoRede);
            Assert.False(salva.MonitorarSnmp);
            Assert.True(await db.LogsAuditoria.AnyAsync(
                l => l.EntidadeId == impressora.Id && l.Acao == AcaoAuditoria.ImpressoraConexaoDefinida));
        }
        finally
        {
            await LimparAsync(db, unidade, impressora.Id);
        }
    }
}
