namespace Ravuno.Fetcher.DntActivities.Settings;

public class DntActivitiesSettings
{
    public string ActivitiesApiUrl { get; set; } = string.Empty;

    public string EventDetailApiUrl { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;

    public bool IsDetailedEnabled { get; set; } = true;
}