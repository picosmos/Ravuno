using Microsoft.EntityFrameworkCore;
using Ravuno.DataStorage;

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
            var pending = context.Database.GetPendingMigrations();
            if (pending.Any())
            {
                logger.LogInformation("Applying {Count} pending migrations...", pending.Count());
                context.Database.Migrate();
                logger.LogInformation("Database migrations applied successfully");
            }

            logger.LogWarning(
                "Application started - this indicates a restart, which should not happen during normal operation. Check previous logs if this was not an intended (re)start."
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while applying database migrations");
            throw;
        }
    }
}
