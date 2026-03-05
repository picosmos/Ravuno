using System.ComponentModel.DataAnnotations;

namespace Ravuno.DataStorage.Models;

public class Query
{
    [Key]
    public long Id { get; set; }

    [Required]
    [MaxLength(500)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string SqlQuery { get; set; } = string.Empty;

    [Required]
    [MaxLength(32)]
    public string PublicId { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Email { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;
}
