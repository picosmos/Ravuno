using System.Threading.RateLimiting;
using DntActivities.Services;
using DntActivities.Services.Contracts;
using Email.Services;
using Email.Services.Contracts;
using Email.Settings;
using Microsoft.AspNetCore.HttpOverrides;
using Tekna.Services;
using Tekna.Services.Contracts;
using WebAPI.Extensions;
using WebAPI.Services;
using WebAPI.Services.Contracts;

var builder = WebApplication.CreateBuilder(args);

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
if (builder.Environment.IsDevelopment())
{
    builder.Logging.AddDebug();
}

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure email settings
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddScoped<IEmailService, EmailService>();

// Configure Tekna service
builder.Services.AddHttpClient<ITeknaFetchService, TeknaFetchService>();

// Configure DNT Activities service
builder.Services.AddHttpClient<IDntActivityFetchService, DntActivityFetchService>();

// Add memory cache
builder.Services.AddMemoryCache();

// Add DataStorage with Entity Framework
builder.Services.AddDataStorage(builder.Configuration);

// Add Update Configuration Service
builder.Services.AddScoped<IUpdateConfigurationService, UpdateConfigurationService>();

// Add Hosted Service for fetching and sending
builder.Services.AddHostedService<FetchAndSendHostedService>();

// Add rate limiting
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("DntApi", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(1),
                PermitLimit = 20, // 20 requests per minute per IP
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 5
            }));
});

// Configure forwarded headers for reverse proxy
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// Add CORS
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

// Apply migrations at startup
app.ApplyDatabaseMigrations();

app.UseForwardedHeaders();

app.UseSwagger();
app.UseSwaggerUI();

app.UseRateLimiter();

app.UseCors();

app.Run();