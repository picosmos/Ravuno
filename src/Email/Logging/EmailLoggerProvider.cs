using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ravuno.Email.Services.Contracts;
using Ravuno.Email.Settings;

namespace Ravuno.Email.Logging;

/// <summary>
/// A custom logger provider that batches log messages and sends them via email.
/// Logs are collected and sent based on configurable time intervals to avoid email spam.
/// Uses PeriodicTimer for efficient periodic checks without creating task overhead on every log call.
/// </summary>
public sealed class EmailLoggerProvider : ILoggerProvider
{
    private readonly IEmailService _emailService;
    private readonly ConcurrentQueue<LogEntry> _logQueue = new();
    private readonly PeriodicTimer _periodicTimer;
    private readonly Task _timerTask;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private DateTime? _firstLogTime;
    private bool _disposed;

    public EmailLoggerProvider(
        IEmailService emailService,
        IOptions<EmailLogProviderSettings> settings)
    {
        ArgumentNullException.ThrowIfNull(emailService);
        ArgumentNullException.ThrowIfNull(settings);

        this._emailService = emailService;
        this.Settings = settings.Value;

        // Use PeriodicTimer for efficient periodic checks without creating tasks on every log
        this._periodicTimer = new PeriodicTimer(this.Settings.CheckInterval);
        this._timerTask = Task.Run(this.RunPeriodicCheckAsync);
    }

    internal EmailLogProviderSettings Settings { get; }
    internal bool IsEnabled => this.Settings.IsEnabled;

    public ILogger CreateLogger(string categoryName)
    {
        return new EmailLogger(categoryName, this);
    }

    /// <summary>
    /// Adds a log entry to the queue for batched sending.
    /// This method is lightweight and doesn't trigger immediate checks - the PeriodicTimer handles scheduling.
    /// </summary>
    internal void AddLog(LogEntry logEntry)
    {
        if (this._disposed)
        {
            return;
        }

        this._logQueue.Enqueue(logEntry);

        // Set first log time if this is the first log
        if (this._firstLogTime == null)
        {
            this._firstLogTime = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Runs the periodic timer loop that checks for logs to send.
    /// </summary>
    private async Task RunPeriodicCheckAsync()
    {
        try
        {
            while (await this._periodicTimer.WaitForNextTickAsync(this._cancellationTokenSource.Token))
            {
                await this.CheckAndSendLogsAsync();
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when disposing
        }
    }

    /// <summary>
    /// Checks if logs should be sent based on timing rules and sends them if conditions are met.
    /// This method is called periodically by the PeriodicTimer.
    /// </summary>
    private async Task CheckAndSendLogsAsync()
    {
        if (this._disposed || !this.Settings.IsEnabled || this._logQueue.IsEmpty)
        {
            return;
        }

        // Prevent concurrent sends (non-blocking check)
        if (!await this._sendLock.WaitAsync(0))
        {
            return;
        }

        try
        {
            var now = DateTime.UtcNow;

            if (this._firstLogTime == null)
            {
                return;
            }

            var timeSinceFirstLog = now - this._firstLogTime.Value;

            // Check if we should send based on MaxWaitTimeBeforeSend
            var shouldSendDueToMaxWait = timeSinceFirstLog >= this.Settings.MaxWaitTimeBeforeSend;

            // Check if we should send based on WaitIntervalBeforeSend (no new logs in the interval)
            var shouldSendDueToInterval = timeSinceFirstLog >= this.Settings.WaitIntervalBeforeSend;

            if (shouldSendDueToMaxWait || shouldSendDueToInterval)
            {
                await this.SendLogsAsync(shouldSendDueToMaxWait);
            }
        }
        finally
        {
            this._sendLock.Release();
        }
    }

    /// <summary>
    /// Sends the batched log entries via email.
    /// </summary>
    /// <param name="isDueToMaxWait">True if sending because MaxWaitTimeBeforeSend was exceeded.</param>
    private async Task SendLogsAsync(bool isDueToMaxWait)
    {
        if (this._logQueue.IsEmpty)
        {
            return;
        }

        var logs = new List<LogEntry>();
        while (this._logQueue.TryDequeue(out var log))
        {
            logs.Add(log);
        }

        if (logs.Count == 0)
        {
            return;
        }

        var sb = new StringBuilder();

        if (isDueToMaxWait)
        {
            sb.AppendLine("⚠️ WARNING: High volume of logs detected. This email contains logs that accumulated over the maximum wait time.");
            sb.AppendLine("This may indicate ongoing issues that require immediate attention.");
            sb.AppendLine();
            sb.AppendLine(new string('=', 80));
            sb.AppendLine();
        }

        sb.AppendLine($"Log Summary Report");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Total Entries: {logs.Count}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Time Range: {logs[0].Timestamp:yyyy-MM-dd HH:mm:ss} UTC to {logs[^1].Timestamp:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();

        var groupedByLevel = logs.GroupBy(l => l.LogLevel).OrderByDescending(g => g.Key);
        foreach (var group in groupedByLevel)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"{group.Key}: {group.Count()} entries");
        }

        sb.AppendLine();
        sb.AppendLine(new string('=', 80));
        sb.AppendLine();
        sb.AppendLine("Detailed Log Entries:");
        sb.AppendLine();

        foreach (var log in logs)
        {
            sb.AppendLine(log.ToString());
            sb.AppendLine(new string('-', 80));
            sb.AppendLine();
        }

        try
        {
            await this._emailService.SendEmailAsync(
                this.Settings.AdminEmailReceiver,
                $"Application Logs - {logs.Count} entries ({logs[0].Timestamp:yyyy-MM-dd HH:mm:ss} UTC to {logs[^1].Timestamp:yyyy-MM-dd HH:mm:ss} UTC)",
                sb.ToString());

            this._firstLogTime = null; // Reset for next batch
        }
        catch (Exception ex)
        {
            // IMPORTANT: Cannot use ILogger here as it would create circular dependency during startup.
            // Writing to Console.Error ensures the error is visible in Docker logs and hosting environments.
            // This is the standard approach for logging infrastructure failures.
            await Console.Error.WriteLineAsync($"[EmailLoggerProvider] Failed to send log email to {this.Settings.AdminEmailReceiver}: {ex.Message}");
        }
    }

    /// <summary>
    /// Disposes the logger provider and attempts to send any remaining logs.
    /// </summary>
    public void Dispose()
    {
        if (this._disposed)
        {
            return;
        }

        this._disposed = true;

        // Cancel the periodic timer and wait for it to complete
        this._cancellationTokenSource.Cancel();
        try
        {
            this._timerTask.Wait(TimeSpan.FromSeconds(5));
        }
        catch (AggregateException)
        {
            // Ignore cancellation exceptions during disposal
        }

        // Try to send remaining logs before disposing
        if (!this._logQueue.IsEmpty)
        {
            try
            {
                this.SendLogsAsync(false).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[EmailLoggerProvider] Failed to send remaining logs during disposal: {ex.Message}");
            }
        }

        this._periodicTimer?.Dispose();
        this._cancellationTokenSource?.Dispose();
        this._sendLock?.Dispose();
    }
}