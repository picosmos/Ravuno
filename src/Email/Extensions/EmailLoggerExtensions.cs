using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ravuno.Email.Logging;
using Ravuno.Email.Settings;

namespace Ravuno.Email.Extensions;

public static class EmailLoggerExtensions
{
    /// <summary>
    /// Adds the email logger provider to the logging infrastructure.
    /// This does not remove any existing log providers.
    /// </summary>
    public static ILoggingBuilder AddEmailLogger(this ILoggingBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddSingleton<EmailLoggerProvider>();
        builder.Services.AddSingleton<ILoggerProvider>(sp =>
            sp.GetRequiredService<EmailLoggerProvider>()
        );

        return builder;
    }

    /// <summary>
    /// Configures and validates the email log provider settings with custom validation logic.
    /// </summary>
    public static IServiceCollection ConfigureAndValidateEmailLoggerSettings(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName
    )
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<EmailLogProviderSettings>(configuration.GetSection(sectionName));
        services
            .AddOptions<EmailLogProviderSettings>()
            .Bind(configuration.GetSection(sectionName))
            .ValidateDataAnnotations()
            .Validate(
                settings =>
                {
                    if (
                        settings.IsEnabled && string.IsNullOrWhiteSpace(settings.AdminEmailReceiver)
                    )
                    {
                        return false;
                    }

                    if (settings.WaitIntervalBeforeSend > settings.MaxWaitTimeBeforeSend)
                    {
                        return false;
                    }

                    if (settings.CheckInterval > settings.WaitIntervalBeforeSend)
                    {
                        return false;
                    }
                    return true;
                },
                "AdminEmailReceiver is required when IsEnabled is true. WaitIntervalBeforeSend must be <= MaxWaitTimeBeforeSend. CheckInterval must be <= WaitIntervalBeforeSend."
            )
            .ValidateOnStart();

        return services;
    }
}
