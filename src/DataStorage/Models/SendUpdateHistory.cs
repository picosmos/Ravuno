using System.ComponentModel.DataAnnotations;

namespace Ravuno.DataStorage.Models;

public class SendUpdateHistory
{
    [Key]
    public long Id { get; set; }

    public required string QueryTitle { get; set; }

    public required string EmailReceiverAddress { get; set; }

    public DateTime SentAt { get; set; }

    public int NewItemsCount { get; set; }

    public int UpdatedItemsCount { get; set; }
}
