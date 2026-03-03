using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Ravuno.DataStorage.Repositories;
using Ravuno.DataStorage.Repositories.Contracts;
using Ravuno.Email.Extensions;
using Ravuno.Email.Services;
using Ravuno.Email.Services.Contracts;
using Ravuno.Email.Settings;
using Ravuno.Fetcher.DntActivities.Services;
using Ravuno.Fetcher.DntActivities.Services.Contracts;
using Ravuno.Fetcher.DntActivities.Settings;
using Ravuno.Fetcher.Tekna.Services;
using Ravuno.Fetcher.Tekna.Services.Contracts;
using Ravuno.Fetcher.Tekna.Settings;
using Ravuno.WebAPI.Extensions;
using Ravuno.WebAPI.Services;
using Ravuno.WebAPI.Services.Contracts;
using Ravuno.WebAPI.Settings;

var builder = WebApplication.CreateBuilder(args);

// Configure thread pool to handle concurrent operations better
// These settings help prevent thread pool starvation
ThreadPool.SetMinThreads(
    workerThreads: Environment.ProcessorCount * 4,
    completionPortThreads: Environment.ProcessorCount * 4
);

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole();

if (builder.Environment.IsDevelopment())
{
    builder.Logging.AddDebug();
}

builder.Services.ConfigureAndValidateEmailLoggerSettings(builder.Configuration, "Logging:Email");
builder.Logging.AddEmailLogger();

builder.Services.ConfigureAndValidateSettings<EmailSettings>(
    builder.Configuration,
    "EmailSettings"
);
builder.Services.AddSingleton<IEmailService, EmailService>();

builder.Services.ConfigureAndValidateSettings<FetchAndSendSettings>(
    builder.Configuration,
    "FetchAndSendSettings"
);
builder.Services.ConfigureAndValidateSettings<TeknaSettings>(
    builder.Configuration,
    "FetcherSettings:Tekna"
);
builder.Services.ConfigureAndValidateSettings<DntActivitiesSettings>(
    builder.Configuration,
    "FetcherSettings:DntActivities"
);
builder.Services.ConfigureAndValidateSettings<CleanupSettings>(
    builder.Configuration,
    "CleanupSettings"
);
builder.Services.ConfigureAndValidateSettings<PasswordSettings>(
    builder.Configuration,
    "PasswordSettings"
);

builder.Services.AddHttpClient<ITeknaFetchService, TeknaFetchService>();
builder.Services.AddHttpClient<IDntActivityFetchService, DntActivityFetchService>();

builder.Services.AddMemoryCache();
builder.Services.AddDataStorage(builder.Configuration);

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IUserService, UserService>();

// Configure data protection to persist keys for future non-readonly features
builder
    .Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo("/app/data/keys"))
    .SetApplicationName("Ravuno");

builder.Services.AddScoped<IUpdateConfigurationService, UpdateConfigurationService>();
builder.Services.AddScoped<FetchAndSendService>();
builder.Services.AddHostedService<FetchAndSendHostedService>();
builder.Services.AddHostedService<ItemCleanupService>();

builder
    .Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
        options.AccessDeniedPath = "/access-denied";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();

builder.Services.AddControllersWithViews();

builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy(
        "RavunoApi",
        context =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    Window = TimeSpan.FromMinutes(1),
                    PermitLimit = 20,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 5,
                }
            )
    );
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/error");
}

app.ApplyDatabaseMigrations();

app.UseForwardedHeaders();

app.UseRateLimiter();

app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(name: "default", pattern: "{controller=Stats}/{action=FetchHistory}/{id?}");

app.Run();
