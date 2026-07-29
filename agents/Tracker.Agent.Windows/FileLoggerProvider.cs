namespace Tracker.Agent.Windows;

/// <summary>
/// Logger simples em arquivo. Essencial para diagnosticar o agente quando roda como Serviço
/// do Windows (sem console visível). Grava em agent.log, ao lado do executável.
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _path;
    private readonly object _lock = new();

    public FileLoggerProvider(string path) => _path = path;

    public ILogger CreateLogger(string categoryName) => new FileLogger(categoryName, _path, _lock);

    public void Dispose() { }

    private sealed class FileLogger(string categoria, string path, object trava) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? ex,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            var nome = categoria.Split('.').Last();
            var linha = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{logLevel}] {nome} - {formatter(state, ex)}";
            if (ex is not null) linha += Environment.NewLine + ex;
            lock (trava)
            {
                try { File.AppendAllText(path, linha + Environment.NewLine); } catch { /* nunca derruba o agente por log */ }
            }
        }
    }
}
