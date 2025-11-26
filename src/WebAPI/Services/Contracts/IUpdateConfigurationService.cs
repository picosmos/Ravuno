using WebAPI.Models;

namespace WebAPI.Services.Contracts;

public interface IUpdateConfigurationService
{
    Task<List<UpdateConfiguration>> GetUpdateConfigurationsAsync();
}