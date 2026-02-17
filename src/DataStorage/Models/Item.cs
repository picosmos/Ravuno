using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Ravuno.DataStorage.Attributes;

namespace Ravuno.DataStorage.Models;

// Ensure unique constraint on (Source, SourceId)
[Index(nameof(Source), nameof(SourceId), IsUnique = true)]
public class Item
{
    [Key]
    public long Id { get; set; }

    [Required]
    public ItemSource Source { get; set; }

    public string? RawData { get; set; }

    [Required]
    public string SourceId { get; set; } = string.Empty;

    public DateTime RetrievedAt { get; set; }

    [LocalTime]
    public DateTime EventStartDateTime { get; set; }

    [LocalTime]
    public DateTime EventEndDateTime { get; set; }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance",
        "CA1819:Properties should not return arrays",
        Justification = "Raw data tags"
    )]
    public string[]? Tags { get; set; }

    public string? Title { get; set; }

    public string? Description { get; set; }

    public string? Organizer { get; set; } = string.Empty;

    public string? Location { get; set; }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA1056:URI-like properties should not be strings",
        Justification = "Entity object with raw data"
    )]
    public string? Url { get; set; }

    public string? Price { get; set; }

    [LocalTime]
    public DateTime EnrollmentDeadline { get; set; }
}
