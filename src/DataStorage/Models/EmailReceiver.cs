using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Ravuno.DataStorage.Models;

[Index(nameof(EmailAddress), IsUnique = true)]
public class EmailReceiver
{
    [Key]
    public long Id { get; set; }

    [Required]
    [MaxLength(500)]
    public string EmailAddress { get; set; } = string.Empty;

    public ICollection<SqlScript> SqlScripts { get; set; } = [];
}
