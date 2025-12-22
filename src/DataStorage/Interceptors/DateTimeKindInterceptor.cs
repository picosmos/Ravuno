using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Ravuno.DataStorage.Attributes;

namespace Ravuno.DataStorage.Interceptors;

/// <summary>
/// Interceptor that ensures consistent DateTime handling for database operations.
/// Default behavior: All DateTime properties are treated as UTC.
/// Exception: Properties marked with <see cref="LocalTimeAttribute"/> are treated as local time.
/// Save operations:
/// - Regular properties: Converts Local/Unspecified DateTimes to UTC and logs a warning. UTC values are stored as-is.
/// - Properties with <see cref="LocalTimeAttribute"/>: Stored as-is. Logs a warning if DateTime.Kind is not Local.
/// Read operations:
/// - Regular properties: DateTime.Kind is set to UTC.
/// - Properties with <see cref="LocalTimeAttribute"/>: DateTime.Kind is set to Local.
/// </summary>
public class DateTimeKindInterceptor : SaveChangesInterceptor, IMaterializationInterceptor
{
    private readonly ILogger<DateTimeKindInterceptor>? _logger;
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> KeyPropertiesCache = new();
    private record DateTimePropertyInfo(PropertyInfo Property, bool HasLocalTimeAttribute);
    private static readonly ConcurrentDictionary<Type, DateTimePropertyInfo[]> PropertyCache = new();

    public DateTimeKindInterceptor(ILogger<DateTimeKindInterceptor>? logger = null)
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

        // Cache key properties lookup
        var keyProperties = KeyPropertiesCache.GetOrAdd(entityType, type =>
        {
            // First, check for properties with [Key] attribute
            var keysWithAttribute = type.GetProperties()
                .Where(p => p.GetCustomAttributes(typeof(KeyAttribute), inherit: true).Length > 0)
                .ToArray();

            if (keysWithAttribute.Length > 0)
            {
                return keysWithAttribute;
            }

            // Fallback to property named "Id"
            var idProperty = type.GetProperty("Id");
            return idProperty != null ? [idProperty] : [];
        });

        if (keyProperties.Length > 0)
        {
            var keyPairs = keyProperties.Select(p => $"{p.Name}={p.GetValue(entity)}");
            return string.Join(", ", keyPairs);
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
                if (property.Metadata.ClrType != typeof(DateTime) && Nullable.GetUnderlyingType(property.Metadata.ClrType) != typeof(DateTime))
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
                            "Value will be stored as-is without conversion. Affected entity key/id: {EntityId}, Value: {DateTime}, Entity State: {EntityState}",
                            entityName,
                            propertyName,
                            dateTime.Value.Kind,
                            entityId,
                            dateTime.Value,
                            entry.State);
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
                        "Please ensure DateTime values are assigned as UTC to avoid ambiguity. Affected entity key/id: {EntityId}, Value: {DateTime}, Entity State: {EntityState}",
                        entityName,
                        propertyName,
                        dateTime.Value,
                        dateTime.Value.ToUniversalTime(),
                        entityId,
                        dateTime.Value,
                        entry.State);

                    property.CurrentValue = dateTime.Value.ToUniversalTime();
                }
                else // DateTimeKind.Unspecified
                {
                    var entityId = GetEntityIdentifier(entry.Entity);

                    this._logger?.LogDebug("Entity: {@Entity}", entry.Entity);
                    this._logger?.LogWarning(
                        "Unspecified DateTime detected for {EntityName}.{PropertyName}. " +
                        "Treating {DateTime} as UTC. " +
                        "Please use DateTime.SpecifyKind or assign values with DateTimeKind.Utc to avoid ambiguity. Affected entity key/id: {EntityId}, Value: {DateTime}, Entity State: {EntityState}",
                        entityName,
                        propertyName,
                        dateTime.Value,
                        entityId,
                        dateTime.Value,
                        entry.State);
                    property.CurrentValue = DateTime.SpecifyKind(dateTime.Value, DateTimeKind.Utc);
                }
            }
        }
    }

    public object CreatedInstance(MaterializationInterceptionData materializationData, object entity)
    {
        return entity;
    }

    public ValueTask<object> CreatedInstanceAsync(MaterializationInterceptionData materializationData, object entity, CancellationToken cancellationToken = default)
    {
        return new ValueTask<object>(entity);
    }

    public object InitializedInstance(MaterializationInterceptionData materializationData, object entity)
    {
        this._logger?.LogDebug("InitializedInstance called for entity type: {EntityType}", entity.GetType().Name);
        this.SetDateTimeKinds(entity);
        return entity;
    }

    public ValueTask<object> InitializedInstanceAsync(MaterializationInterceptionData materializationData, object entity, CancellationToken cancellationToken = default)
    {
        this._logger?.LogDebug("InitializedInstanceAsync called for entity type: {EntityType}", entity.GetType().Name);
        this.SetDateTimeKinds(entity);
        return new ValueTask<object>(entity);
    }

    private void SetDateTimeKinds(object entity)
    {
        var entityType = entity.GetType();
        
        // Cache property metadata lookup
        var dateTimeProperties = PropertyCache.GetOrAdd(entityType, type =>
        {
            return type.GetProperties()
                .Where(p => (p.PropertyType == typeof(DateTime) || Nullable.GetUnderlyingType(p.PropertyType) == typeof(DateTime)) 
                         && p.CanRead && p.CanWrite)
                .Select(p => new DateTimePropertyInfo(
                    p,
                    p.GetCustomAttributes(typeof(LocalTimeAttribute), inherit: true).Length > 0))
                .ToArray();
        });

        foreach (var propInfo in dateTimeProperties)
        {
            var dateTime = (DateTime?)propInfo.Property.GetValue(entity);
            
            if (!dateTime.HasValue || dateTime == DateTime.MaxValue || dateTime == DateTime.MinValue)
            {
                continue;
            }

            var targetKind = propInfo.HasLocalTimeAttribute ? DateTimeKind.Local : DateTimeKind.Utc;
            
            if (dateTime.Value.Kind != targetKind)
            {
                this._logger?.LogDebug(
                    "Setting DateTime.Kind for {EntityType}.{PropertyName} from {FromKind} to {ToKind}. Value: {DateTime}",
                    entityType.Name,
                    propInfo.Property.Name,
                    dateTime.Value.Kind,
                    targetKind,
                    dateTime.Value);
                    
                propInfo.Property.SetValue(entity, DateTime.SpecifyKind(dateTime.Value, targetKind));
            }
        }
    }
}