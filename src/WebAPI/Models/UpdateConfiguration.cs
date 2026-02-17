namespace Ravuno.WebAPI.Models;

public class UpdateConfiguration
{
    public required long Id { get; set; }
    public required List<string> EmailReceiverAddresses { get; set; } = [];
    public required string QueryTitle { get; set; }
    public required string SqlQuery { get; set; }
}
