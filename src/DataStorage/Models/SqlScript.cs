using System.ComponentModel.DataAnnotations;

namespace Ravuno.DataStorage.Models;

public class SqlScript
{
    [Key]
    public long Id { get; set; }

    [Required]
    [MaxLength(500)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Query { get; set; } = string.Empty;

    public ICollection<EmailReceiver> EmailReceivers { get; set; } = [];
}
