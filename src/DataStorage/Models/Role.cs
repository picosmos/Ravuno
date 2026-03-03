using System.ComponentModel.DataAnnotations;

namespace Ravuno.DataStorage.Models;

public class Role
{
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    public ICollection<User> Users { get; set; } = [];
}
