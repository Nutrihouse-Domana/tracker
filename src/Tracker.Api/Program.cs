using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Tracker.Api.Agente;
using Tracker.Api.Json;
using Tracker.Application.Validators;
using Tracker.Infrastructure.Data;
using Tracker.Infrastructure.Identity;
using Tracker.Infrastructure.Pdf;
using Tracker.Infrastructure.Security;
using Tracker.Infrastructure.Storage;

var builder = WebApplication.CreateBuilder(args);

// O utilitário de recuperação de acesso (--redefinir-senha-adm) é rodado à mão no terminal, onde
// o ambiente normalmente NÃO é Development — e aí o .NET não carrega os user-secrets, fazendo o
// comando falhar na chave de criptografia antes de chegar à redefinição. Carregamos os secrets
// explicitamente para que a recuperação não dependa de lembrar de ASPNETCORE_ENVIRONMENT.
// Em produção o servidor não tem secrets: a chamada simplesmente não encontra nada.
if (args.Contains("--redefinir-senha-adm"))
{
    builder.Configuration.AddUserSecrets<Program>(optional: true);
}

// Add services to the container.

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        // Emite todo DateTime como UTC com 'Z' (senão o front lê UTC como hora local: horário 3h adiantado).
        options.JsonSerializerOptions.Converters.Add(new UtcDateTimeConverter());
    });
// O SignalR tem serialização própria: o JsonStringEnumConverter registrado em AddControllers NÃO
// vale aqui. Sem esta linha o hub envia TipoComando como número (0/1) enquanto o heartbeat envia
// como texto ("Bloquear"), e os agentes — que esperam texto — descartam o comando em silêncio.
// O efeito era um bloqueio que só chegava no heartbeat seguinte, em vez de ser imediato.
builder.Services.AddSignalR().AddJsonProtocol(options =>
{
    options.PayloadSerializerOptions.Converters.Add(
        new System.Text.Json.Serialization.JsonStringEnumConverter());
});
builder.Services.AddValidatorsFromAssemblyContaining<CriarUsuarioRequestValidator>();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Chave de criptografia do CPF: em dev, "Encryption:CpfKeyBase64" (32 bytes, base64) vem de
// appsettings.Development.json / user-secrets. Em produção, mover para um cofre de segredos.
var cpfKeyBase64 = builder.Configuration["Encryption:CpfKeyBase64"];

if (string.IsNullOrWhiteSpace(cpfKeyBase64))
{
    // A recuperação de acesso só toca a tabela de usuários ADM — nunca lê nem grava CPF. Uma
    // chave efêmera satisfaz o DbContext e evita que recuperar o login dependa de configuração
    // presente, que é justamente o que costuma faltar na hora do aperto.
    if (!args.Contains("--redefinir-senha-adm"))
    {
        throw new InvalidOperationException(
            "Configuração ausente: Encryption:CpfKeyBase64. Gere uma chave AES-256 (32 bytes) em base64.");
    }

    cpfKeyBase64 = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
}

builder.Services.AddSingleton(new AesStringEncryptor(Convert.FromBase64String(cpfKeyBase64)));

builder.Services.AddDbContext<TrackerDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("TrackerDb"))
        // Dispositivo tem filtro global (exclusão lógica) e é o lado obrigatório de várias
        // relações (movimentações, reparos, termos...). O EF avisa disso a cada start; aqui é
        // intencional: o histórico de um ativo arquivado só é lido com IgnoreQueryFilters().
        .ConfigureWarnings(w => w.Ignore(
            Microsoft.EntityFrameworkCore.Diagnostics.CoreEventId
                .PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning)));

builder.Services
    .AddIdentity<UsuarioADM, IdentityRole<Guid>>(options =>
    {
        options.Password.RequiredLength = 10;
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<TrackerDbContext>()
    .AddDefaultTokenProviders();

// A API é consumida por um SPA: chamadas não autenticadas devem retornar 401/403 (JSON),
// e não redirecionar para a tela de login do Identity (/Account/Login). Sem isso, o redirect
// 302 cai no fallback do SPA (index.html com 200) e o front acha que está autenticado.
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});

builder.Services.AddScoped<ITermoDeUsoPdfService, TermoDeUsoPdfService>();
builder.Services.AddSingleton<AgentTokenService>();
builder.Services.AddScoped<AgentAuth>();
builder.Services.AddScoped<Tracker.Api.Seguranca.ConfirmacaoSenhaAdm>();

var termosPath = Path.Combine(
    builder.Environment.ContentRootPath,
    builder.Configuration["Storage:TermosDeUsoPath"] ?? "App_Data/termos-de-uso");
builder.Services.AddSingleton<IArmazenamentoArquivosService>(_ => new ArmazenamentoArquivosService(termosPath));

var pacotesPath = Path.Combine(
    builder.Environment.ContentRootPath,
    builder.Configuration["Storage:PacotesAgentePath"] ?? "App_Data/pacotes-agente");
builder.Services.AddSingleton<IArmazenamentoPacotesAgenteService>(
    _ => new ArmazenamentoPacotesAgenteService(pacotesPath));

builder.Services.AddCors(options =>
{
    options.AddPolicy("TrackerWeb", policy =>
    {
        policy.WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    try
    {
        // Aplica as migrações pendentes automaticamente (cria/atualiza o schema no primeiro
        // start do servidor, sem precisar da ferramenta dotnet-ef instalada em produção).
        await scope.ServiceProvider.GetRequiredService<TrackerDbContext>().Database.MigrateAsync();
        await DbSeeder.SeedAsync(scope.ServiceProvider);
    }
    catch (Exception ex)
    {
        // Não derruba o startup se o banco ainda não estiver disponível (ex.: primeiro
        // provisionamento, ou ambiente de teste sem SQL Server real).
        scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("DbSeeder")
            .LogWarning(ex, "Não foi possível migrar/semear o banco — o banco de dados está disponível?");
    }
}

// Recuperação de acesso: "dotnet run -- --redefinir-senha-adm" aplica Seed:AdmSenha ao ADM
// existente e encerra, sem subir o servidor. Fica atrás de um argumento explícito para que um
// start normal (ou o IIS em produção) nunca redefina senha nenhuma.
if (args.Contains("--redefinir-senha-adm"))
{
    // A senha pode vir por argumento (--senha ...), que dispensa configurar o cofre antes, ou
    // de Seed:AdmSenha quando o argumento não é passado.
    static string? ValorDe(string[] todos, string nome)
    {
        var i = Array.IndexOf(todos, nome);
        return i >= 0 && i + 1 < todos.Length ? todos[i + 1] : null;
    }

    using var escopoSenha = app.Services.CreateScope();
    var ok = await DbSeeder.RedefinirSenhaAdmAsync(
        escopoSenha.ServiceProvider,
        email: ValorDe(args, "--email"),
        senha: ValorDe(args, "--senha"));
    Console.WriteLine(ok
        ? "Senha do ADM redefinida. Entre no site com o e-mail e a senha de Seed:*."
        : "Não foi possível redefinir a senha — veja o erro acima.");
    return ok ? 0 : 1;
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Serve o site (build do React em wwwroot) no mesmo endereço da API — evita CORS em produção.
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseCors("TrackerWeb");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<ComandoRemotoHub>("/hubs/comandos");
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// Rotas do SPA (client-side) caem no index.html; /api, /hubs e /health têm prioridade.
app.MapFallbackToFile("index.html");

app.Run();

// Exigido porque o atalho --redefinir-senha-adm retorna um código de saída: com um return
// tipado no arquivo, todos os caminhos precisam devolver um valor.
return 0;

public partial class Program { }
