namespace DataStorage.Models;

public class SendUpdateHistory
{
    public int Id { get; set; }

    public required string QueryTitle { get; set; }

    public required string EmailReceiverAddress { get; set; }

    public DateTime SentAt { get; set; }

    public int NewItemsCount { get; set; }

    public int UpdatedItemsCount { get; set; }
}
