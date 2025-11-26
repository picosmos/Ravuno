namespace WebAPI.Models;

public class UpdateConfiguration
{
    public required string EmailReceiverAddress { get; set; }
    public required string QueryTitle { get; set; }
    public required string SqlQuery { get; set; }
    public required string FilePath { get; set; }
}