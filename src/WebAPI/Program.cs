using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
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

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
if (builder.Environment.IsDevelopment())
{
    builder.Logging.AddDebug();
}

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddScoped<IEmailService, EmailService>();

builder.Services.Configure<FetchAndSendSettings>(builder.Configuration.GetSection("FetchAndSendSettings"))
    .AddOptions<FetchAndSendSettings>()
    .Bind(builder.Configuration.GetSection("FetchAndSendSettings"))
    .ValidateDataAnnotations()
    .Validate(settings =>
    {
        var validSources = Enum.GetValues<Ravuno.DataStorage.Models.ItemSource>();
        var invalidSources = settings.EnabledSources.Except(validSources).ToList();
        return invalidSources.Count > 0
            ? throw new InvalidOperationException(
                $"Invalid sources configured: {string.Join(", ", invalidSources)}. " +
                $"Valid sources are: {string.Join(", ", validSources)}")
            : true;
    }, "EnabledSources must only contain valid ItemSource values")
    .ValidateOnStart();
builder.Services.Configure<TeknaSettings>(builder.Configuration.GetSection("TeknaSettings"));
builder.Services.Configure<DntActivitiesSettings>(builder.Configuration.GetSection("DntActivitiesSettings"));
builder.Services.Configure<CleanupSettings>(builder.Configuration.GetSection("CleanupSettings"))
    .AddOptions<CleanupSettings>()
    .Bind(builder.Configuration.GetSection("CleanupSettings"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddHttpClient<ITeknaFetchService, TeknaFetchService>();
builder.Services.AddHttpClient<IDntActivityFetchService, DntActivityFetchService>();

builder.Services.AddMemoryCache();
builder.Services.AddDataStorage(builder.Configuration);

builder.Services.AddScoped<IUpdateConfigurationService, UpdateConfigurationService>();
builder.Services.AddHostedService<FetchAndSendHostedService>();
builder.Services.AddHostedService<ItemCleanupService>();

builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("DntApi", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(1),
                PermitLimit = 20,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 5
            }));
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
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

app.UseSwagger();
app.UseSwaggerUI();

app.UseRateLimiter();

app.UseCors();

app.Run();