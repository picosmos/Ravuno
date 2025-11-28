using System.Threading.RateLimiting;
using DntActivities.Services;
using DntActivities.Services.Contracts;
using DntActivities.Settings;
using Email.Services;
using Email.Services.Contracts;
using Email.Settings;
using Microsoft.AspNetCore.HttpOverrides;
using Tekna.Services;
using Tekna.Services.Contracts;
using Tekna.Settings;
using WebAPI.Extensions;
using WebAPI.Services;
using WebAPI.Services.Contracts;
using WebAPI.Settings;

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