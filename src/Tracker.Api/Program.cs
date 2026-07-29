using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Tracker.Api.Agente;
using Tracker.Application.Validators;
using Tracker.Infrastructure.Data;
using Tracker.Infrastructure.Identity;
using Tracker.Infrastructure.Pdf;
using Tracker.Infrastructure.Security;
using Tracker.Infrastructure.Storage;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddSignalR();
builder.Services.AddValidatorsFromAssemblyContaining<CriarUsuarioRequestValidator>();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Chave de criptografia do CPF: em dev, "Encryption:CpfKeyBase64" (32 bytes, base64) vem de
// appsettings.Development.json / user-secrets. Em produção, mover para um cofre de segredos.
var cpfKeyBase64 = builder.Configuration["Encryption:CpfKeyBase64"]
    ?? throw new InvalidOperationException(
        "Configuração ausente: Encryption:CpfKeyBase64. Gere uma chave AES-256 (32 bytes) em base64.");
builder.Services.AddSingleton(new AesStringEncryptor(Convert.FromBase64String(cpfKeyBase64)));

builder.Services.AddDbContext<TrackerDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("TrackerDb")));

builder.Services
    .AddIdentity<UsuarioADM, IdentityRole<Guid>>(options =>
    {
        options.Password.RequiredLength = 10;
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<TrackerDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddScoped<ITermoDeUsoPdfService, TermoDeUsoPdfService>();
builder.Services.AddSingleton<AgentTokenService>();
builder.Services.AddScoped<AgentAuth>();

var termosPath = Path.Combine(
    builder.Environment.ContentRootPath,
    builder.Configuration["Storage:TermosDeUsoPath"] ?? "App_Data/termos-de-uso");
builder.Services.AddSingleton<IArmazenamentoArquivosService>(_ => new ArmazenamentoArquivosService(termosPath));

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

public partial class Program { }
