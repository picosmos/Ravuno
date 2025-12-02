using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Ravuno.Email.Logging;

/// <summary>
/// Represents a captured log entry that will be sent via email.
/// Contains all the information needed to reconstruct the original log message.
/// </summary>
internal sealed class LogEntry
{
    public DateTime Timestamp { get; set; }
    public LogLevel LogLevel { get; set; }
    public string Category { get; set; } = string.Empty;
    public EventId EventId { get; set; }
    public string Message { get; set; } = string.Empty;
    public Exception? Exception { get; set; }

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"[{this.Timestamp:yyyy-MM-dd HH:mm:ss.fff} UTC] [{this.LogLevel}] {this.Category}");
        if (this.EventId.Id != 0)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"EventId: {this.EventId}");
        }
        sb.AppendLine(this.Message);
        if (this.Exception != null)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"Exception: {this.Exception}");
        }
        return sb.ToString();
    }
}