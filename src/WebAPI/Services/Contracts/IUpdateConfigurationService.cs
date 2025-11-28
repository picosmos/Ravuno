using Ravuno.WebAPI.Models;

namespace Ravuno.WebAPI.Services.Contracts;

public interface IUpdateConfigurationService
{
    Task<List<UpdateConfiguration>> GetUpdateConfigurationsAsync();
}