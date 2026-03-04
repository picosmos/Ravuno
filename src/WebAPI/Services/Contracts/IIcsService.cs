using Ravuno.DataStorage.Models;

namespace Ravuno.WebAPI.Services.Contracts;

public interface IIcsService
{
    string BuildIcsFeed(List<Item> items);
}
