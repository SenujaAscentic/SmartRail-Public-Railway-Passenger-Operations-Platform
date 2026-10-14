using Microsoft.Extensions.Logging;

namespace SmartRail.IntegrationTests;

// Collects the messages one class writes to the log while a test application starts.
public sealed class LogCapture(string categoryEndsWith, LogLevel minimumLevel) : ILoggerProvider
{
    private readonly LogLevel _minimumLevel = minimumLevel;
    private readonly List<(LogLevel Level, string Message)> _messages = [];

    public IReadOnlyList<(LogLevel Level, string Message)> Messages
    {
        get { lock (_messages) { return [.. _messages]; } }
    }

    public ILogger CreateLogger(string categoryName) =>
        categoryName.EndsWith(categoryEndsWith, StringComparison.Ordinal) ? new Logger(this) : NullLogger();

    public void Dispose() { }

    private static ILogger NullLogger() => Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

    private sealed class Logger(LogCapture owner) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel >= owner._minimumLevel;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            lock (owner._messages)
            {
                owner._messages.Add((logLevel, formatter(state, exception)));
            }
        }
    }
}
