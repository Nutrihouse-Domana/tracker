using Tracker.Agent.Windows;

// Modo tela de bloqueio: lançado na sessão do usuário pela tarefa agendada (não sobe o serviço).
if (args.Contains("--overlay"))
{
    LockOverlay.Executar();
    return;
}

// ContentRoot fixado na pasta do executável. Sem isto, ao rodar como Serviço do Windows o
// diretório atual é System32 e o appsettings.json (que fica junto do .exe) não seria encontrado.
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
});

// Permite rodar como Serviço do Windows (produção) ou como console (teste).
builder.Services.AddWindowsService(options => options.ServiceName = "TrackerAgent");

// Log em arquivo (agent.log) — permite diagnosticar o agente rodando como serviço.
builder.Logging.AddProvider(new FileLoggerProvider(Path.Combine(AppContext.BaseDirectory, "agent.log")));

builder.Services.Configure<AgenteOptions>(builder.Configuration.GetSection("Agente"));
builder.Services.AddHttpClient();
builder.Services.AddSingleton<TokenHolder>();
builder.Services.AddSingleton<EnrollmentService>();
builder.Services.AddSingleton<Geolocalizacao>();
builder.Services.AddSingleton<BackendClient>();
builder.Services.AddSingleton<ExecutorDeComandos>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
