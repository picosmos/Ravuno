using DataStorage;
using Microsoft.EntityFrameworkCore;

namespace WebAPI.Extensions;

public static class DataStorageServiceExtensions
{
    public static IServiceCollection AddDataStorage(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DataStorage")
            ?? "Data Source=ravuno.db";

        services.AddDbContext<DataStorageContext>(options =>
            options.UseSqlite(connectionString));

        return services;
    }
}