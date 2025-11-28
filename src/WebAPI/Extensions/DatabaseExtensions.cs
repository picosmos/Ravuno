using Ravuno.DataStorage;
using Microsoft.EntityFrameworkCore;

namespace Ravuno.WebAPI.Extensions;

public static class DatabaseExtensions
{
    public static void ApplyDatabaseMigrations(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        using var scope = app.ApplicationServices.CreateScope();
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILogger<Program>>();

        try
        {
            var context = services.GetRequiredService<DataStorageContext>();
            logger.LogInformation("Applying database migrations...");
            context.Database.Migrate();
            logger.LogInformation("Database migrations applied successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while applying database migrations");
            throw;
        }
    }
}
