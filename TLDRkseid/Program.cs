using DotNetEnv;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Runtime.Loader;
using TLDRkseid;
using TLDRkseid.Services.HealthChecks;

Env.Load(); // Loads from .env by default

var token = Environment.GetEnvironmentVariable("DISCORD_BOT_TOKEN");
if (string.IsNullOrWhiteSpace(token))
{
    Console.WriteLine("Missing DISCORD_BOT_TOKEN in environment variables.");
    return;
}

// Create and initialize the Discord bot startup
var startup = new Startup();
await startup.InitializeAsync();

// Build minimal web API for health checks
var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel to use PORT environment variable (Railway sets this)
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(int.Parse(port));
});

// Register health checks
builder.Services.AddSingleton(startup.Client);
builder.Services.AddHealthChecks()
    .AddCheck<DiscordHealthCheck>("discord", tags: new[] { "ready", "live" })
    .AddCheck<DatabaseHealthCheck>("database", tags: new[] { "ready" })
    .AddCheck<OpenAIHealthCheck>("openai", tags: new[] { "ready" });

// Add the DbContextFactory for health checks
builder.Services.AddDbContextFactory<TLDRkseid.Data.TldrDbContext>(options =>
{
    var databasePath = Environment.GetEnvironmentVariable("DATABASE_PATH") ?? "tldr.sqlite";
    options.UseSqlite($"Data Source={databasePath}");
});

var app = builder.Build();

// Health check endpoints
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
    ResultStatusCodes =
    {
        [HealthStatus.Healthy] = StatusCodes.Status200OK,
        [HealthStatus.Degraded] = StatusCodes.Status200OK,
        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
    }
});

// Liveness probe (just checks if app is running)
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live"),
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

// Readiness probe (checks all dependencies)
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

// Simple status endpoint
app.MapGet("/", () => new
{
    status = "TLDRkseid Bot Running",
    version = "1.0.0",
    timestamp = DateTime.UtcNow,
    endpoints = new
    {
        health = "/health",
        liveness = "/health/live",
        readiness = "/health/ready"
    }
});

// Start Discord bot
await startup.StartAsync(token);

Console.WriteLine($"Health check server running on port {port}");
Console.WriteLine("Endpoints: /health, /health/live, /health/ready");

// Handle shutdown signals
var waitForStop = new TaskCompletionSource<bool>();

AssemblyLoadContext.Default.Unloading += _ =>
{
    waitForStop.TrySetResult(true);
};

Console.CancelKeyPress += (sender, eventArgs) =>
{
    eventArgs.Cancel = true;
    waitForStop.TrySetResult(true);
};

// Run web server in background
var webTask = app.RunAsync();

// Wait for shutdown signal
await waitForStop.Task;

// Graceful shutdown
await startup.StopAsync();
await app.StopAsync();
