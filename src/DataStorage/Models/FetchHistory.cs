namespace DataStorage.Models;

public class FetchHistory
{
    public int Id { get; set; }

    public ItemSource Source { get; set; }

    public DateTime ExecutionStartTime { get; set; }

    public TimeSpan ExecutionDuration { get; set; }

    public int ItemsRetrieved { get; set; }

    public int NewItems { get; set; }

    public int UpdatedItems { get; set; }
}