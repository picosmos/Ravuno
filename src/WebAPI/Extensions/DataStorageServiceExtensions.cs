using Microsoft.EntityFrameworkCore;
using Ravuno.DataStorage;
using Ravuno.DataStorage.Interceptors;

namespace Ravuno.WebAPI.Extensions;

public static class DataStorageServiceExtensions
{
    public static IServiceCollection AddDataStorage(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var connectionString =
            configuration.GetConnectionString("DataStorage") ?? "Data Source=ravuno.db";

        // Configure SQLite for better concurrency and performance
        // Cache=Shared allows multiple connections to share cache
        // Pooling is enabled by default in modern Microsoft.Data.Sqlite
        if (!connectionString.Contains("Cache=", StringComparison.OrdinalIgnoreCase))
        {
            connectionString += connectionString.Contains('?') ? "&Cache=Shared" : ";Cache=Shared";
        }

        services.AddSingleton<DateTimeKindInterceptor>();

        services.AddDbContext<DataStorageContext>(
            (serviceProvider, options) =>
            {
                var interceptor = serviceProvider.GetRequiredService<DateTimeKindInterceptor>();
                options.UseSqlite(connectionString).AddInterceptors(interceptor);
            }
        );

        return services;
    }
}
