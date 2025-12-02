using Ravuno.DataStorage.Models;

namespace Ravuno.Core.Extensions;

public static class ItemExtensions
{
    /// <summary>
    /// Determines if two items represent the same event based on their identity properties.
    /// Compares Source, Title, SourceId, and event dates (date portion only).
    /// </summary>
    public static bool IsEqualTo(this Item item1, Item item2)
    {
        ArgumentNullException.ThrowIfNull(item1);
        ArgumentNullException.ThrowIfNull(item2);

        return item1.Source == item2.Source &&
               item1.Title == item2.Title &&
               item1.EventEndDateTime.Date == item2.EventEndDateTime.Date &&
               item1.EventStartDateTime.Date == item2.EventStartDateTime.Date &&
               item1.SourceId == item2.SourceId;
    }

    /// <summary>
    /// Determines if two items are completely identical, including all content properties.
    /// Checks identity (via IsEqualTo) plus Price, Description, Location, Url, and exact timestamps.
    /// </summary>
    public static bool IsFullyEqualTo(this Item item1, Item item2)
    {
        ArgumentNullException.ThrowIfNull(item1);
        ArgumentNullException.ThrowIfNull(item2);

        return item1.IsEqualTo(item2) &&
               item1.EventStartDateTime == item2.EventStartDateTime &&
               item1.EventEndDateTime == item2.EventEndDateTime &&
               item1.Price == item2.Price &&
               item1.Description == item2.Description &&
               item1.Location == item2.Location &&
               item1.EnrollmentDeadline.Date == item2.EnrollmentDeadline.Date &&
               item1.Url == item2.Url;
    }
}