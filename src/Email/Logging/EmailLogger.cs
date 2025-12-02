using Microsoft.Extensions.Logging;

namespace Ravuno.Email.Logging;

/// <summary>
/// Logger implementation that captures log messages for batched email sending.
/// Automatically filters out logs from EmailLoggerProvider itself to prevent recursion.
/// </summary>
internal sealed class EmailLogger : ILogger
{
    private readonly string _categoryName;
    private readonly EmailLoggerProvider _provider;

    public EmailLogger(string categoryName, EmailLoggerProvider provider)
    {
        this._categoryName = categoryName;
        this._provider = provider;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return default;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        // Prevent recursion: Don't capture logs from EmailLoggerProvider itself
        // This allows EmailLoggerProvider to safely log errors without creating an infinite loop
        if (this._categoryName.Contains("EmailLoggerProvider", StringComparison.Ordinal))
        {
            return false;
        }

        return this._provider.IsEnabled && logLevel >= this._provider.Settings.MinimumLogLevelToSend;
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!this.IsEnabled(logLevel))
        {
            return;
        }

        var message = formatter(state, exception);
        var logEntry = new LogEntry
        {
            Timestamp = DateTime.UtcNow,
            LogLevel = logLevel,
            Category = this._categoryName,
            EventId = eventId,
            Message = message,
            Exception = exception
        };

        this._provider.AddLog(logEntry);
    }
}