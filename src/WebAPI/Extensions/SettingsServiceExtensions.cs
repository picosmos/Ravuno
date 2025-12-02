namespace Ravuno.WebAPI.Extensions;

public static class SettingsServiceExtensions
{
    /// <summary>
    /// Configures settings with automatic binding, validation, and startup validation.
    /// </summary>
    /// <typeparam name="TSettings">The settings class type</typeparam>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The configuration</param>
    /// <param name="sectionName">The configuration section name</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection ConfigureAndValidateSettings<TSettings>(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName)
        where TSettings : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<TSettings>(configuration.GetSection(sectionName))
            .AddOptions<TSettings>()
            .Bind(configuration.GetSection(sectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }
}
