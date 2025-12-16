using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Ravuno.DataStorage.Attributes;

namespace Ravuno.DataStorage.Interceptors;

/// <summary>
/// Interceptor that ensures all DateTime properties are stored as UTC in the database.
/// Logs warnings when non-UTC DateTime values are detected and automatically converts them to UTC.
/// Properties marked with <see cref="LocalTimeAttribute"/> will skip UTC conversion and log a warning if the DateTime.Kind is not Local.
/// </summary>
public class UtcDateTimeInterceptor : SaveChangesInterceptor
{
    private readonly ILogger<UtcDateTimeInterceptor>? _logger;

    public UtcDateTimeInterceptor(ILogger<UtcDateTimeInterceptor>? logger = null)
    {
        this._logger = logger;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        if (eventData.Context is not null)
        {
            this.ConvertDateTimesToUtc(eventData.Context);
        }

        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        if (eventData.Context is not null)
        {
            this.ConvertDateTimesToUtc(eventData.Context);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static string? GetEntityIdentifier(object entity)
    {
        var entityType = entity.GetType();

        // First, check for properties with [Key] attribute
        var keyProperties = entityType.GetProperties()
            .Where(p => p.GetCustomAttributes(typeof(KeyAttribute), inherit: true).Length > 0)
            .ToList();

        if (keyProperties.Count > 0)
        {
            var keyPairs = keyProperties
                .Select(p => $"{p.Name}={p.GetValue(entity)}");
            return string.Join(", ", keyPairs);
        }

        // Fallback to property named "Id"
        var idProperty = entityType.GetProperty("Id");
        if (idProperty != null)
        {
            var idValue = idProperty.GetValue(entity);
            return $"Id={idValue}";
        }

        return null;
    }

    private void ConvertDateTimesToUtc(DbContext context)
    {
        var entries = context.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified);

        foreach (var entry in entries)
        {
            var entityName = entry.Metadata.ClrType.Name;

            foreach (var property in entry.Properties)
            {
                if (property.Metadata.ClrType != typeof(DateTime))
                {
                    continue;
                }

                var propertyName = property.Metadata.Name;
                var dateTime = (DateTime?)property.CurrentValue;

                if (!dateTime.HasValue || dateTime == DateTime.MaxValue || dateTime == DateTime.MinValue)
                {
                    continue;
                }

                // Check if property has [LocalTime] attribute - skip conversion if it does
                var clrProperty = entry.Entity.GetType().GetProperty(propertyName);
                if (clrProperty?.GetCustomAttributes(typeof(LocalTimeAttribute), inherit: true).Length > 0)
                {
                    if (dateTime.Value.Kind != DateTimeKind.Local)
                    {
                        var entityId = GetEntityIdentifier(entry.Entity);

                        this._logger?.LogDebug("Entity: {@Entity}", entry.Entity);
                        this._logger?.LogWarning(
                            "[LocalTime] attribute found on {EntityName}.{PropertyName}, but DateTime has Kind={Kind}. " +
                            "Expected DateTimeKind.Local for properties marked with [LocalTime]. " +
                            "Value will be stored as-is without conversion. Affected entity key/id: {EntityId}",
                            entityName,
                            propertyName,
                            dateTime.Value.Kind,
                            entityId);
                    }

                    continue;
                }

                if (dateTime.Value.Kind == DateTimeKind.Utc)
                {
                    continue;
                }

                if (dateTime.Value.Kind == DateTimeKind.Local)
                {
                    var entityId = GetEntityIdentifier(entry.Entity);

                    this._logger?.LogDebug("Entity: {@Entity}", entry.Entity);
                    this._logger?.LogWarning(
                        "Local DateTime detected for {EntityName}.{PropertyName}. " +
                        "Converting from {LocalTime} to UTC {UtcTime}. " +
                        "Please ensure DateTime values are assigned as UTC to avoid ambiguity. Affected entity key/id: {EntityId}",
                        entityName,
                        propertyName,
                        dateTime.Value,
                        dateTime.Value.ToUniversalTime(),
                        entityId);

                    property.CurrentValue = dateTime.Value.ToUniversalTime();
                }
                else // DateTimeKind.Unspecified
                {
                    var entityId = GetEntityIdentifier(entry.Entity);

                    this._logger?.LogDebug("Entity: {@Entity}", entry.Entity);
                    this._logger?.LogWarning(
                        "Unspecified DateTime detected for {EntityName}.{PropertyName}. " +
                        "Treating {DateTime} as UTC. " +
                        "Please use DateTime.SpecifyKind or assign values with DateTimeKind.Utc to avoid ambiguity. Affected entity key/id: {EntityId}",
                        entityName,
                        propertyName,
                        dateTime.Value,
                        entityId);

                    property.CurrentValue = DateTime.SpecifyKind(dateTime.Value, DateTimeKind.Utc);
                }
            }
        }
    }
}