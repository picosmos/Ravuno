using Microsoft.EntityFrameworkCore;
using Ravuno.DataStorage;
using Ravuno.DataStorage.Interceptors;

namespace Ravuno.WebAPI.Extensions;

public static class DataStorageServiceExtensions
{
    public static IServiceCollection AddDataStorage(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DataStorage")
            ?? "Data Source=ravuno.db";

        services.AddSingleton<UtcDateTimeInterceptor>();

        services.AddDbContext<DataStorageContext>((serviceProvider, options) =>
        {
            var interceptor = serviceProvider.GetRequiredService<UtcDateTimeInterceptor>();
            options.UseSqlite(connectionString)
                   .AddInterceptors(interceptor);
        });

        return services;
    }
}