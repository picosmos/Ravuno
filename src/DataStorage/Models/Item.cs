using System.ComponentModel.DataAnnotations;

namespace Ravuno.DataStorage.Models;

public class Item
{
    [Key]
    public long Id { get; set; }

    public ItemSource Source { get; set; }

    public string? RawData { get; set; }

    public string SourceId { get; set; } = string.Empty;

    public DateTime RetrievedAt { get; set; }

    public DateTime EventStartDateTime { get; set; }

    public DateTime EventEndDateTime { get; set; }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "Raw data tags")]
    public string[]? Tags { get; set; }

    public string? Title { get; set; }

    public string? Description { get; set; }

    public string? Organizer { get; set; } = string.Empty;

    public string? Location { get; set; }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "Entity object with raw data")]
    public string? Url { get; set; }

    public string? Price { get; set; }

    public DateTime EnrollmentDeadline { get; set; }
}