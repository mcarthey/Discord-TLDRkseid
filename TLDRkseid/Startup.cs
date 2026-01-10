using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using TLDRkseid.Commands;
using TLDRkseid.Configuration;
using TLDRkseid.Data;
using TLDRkseid.Handlers;
using TLDRkseid.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Extensions.Logging;
using OpenAI;
using OpenAI.Interfaces;
using OpenAI.Managers;

namespace TLDRkseid;

public class Startup
{
    public DiscordSocketClient Client { get; private set; } = default!;
    public InteractionService Interactions { get; private set; } = default!;
    public MessageCollectorService Collector { get; private set; } = default!;
    public SummaryLogger Logger { get; private set; } = default!;
    private IServiceProvider _services = default!;
    private ILogger<Startup>? _logger;
    private bool _eventsRegistered = false;
    private Timer? _hourlyTimer;
    private Timer? _dailyTimer;

    public async Task InitializeAsync()
    {
        // Load NLog config from application base directory (works for both Windows and Linux/Docker)
        var nlogConfigPath = Path.Combine(AppContext.BaseDirectory, "NLog.config");
        NLog.LogManager.Setup().LoadConfigurationFromFile(nlogConfigPath);

        // Load application configuration from appsettings.json
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        // Ensure logs directory exists for NLog
        var logsPath = Path.Combine(AppContext.BaseDirectory, "logs");
        Directory.CreateDirectory(logsPath);

        // Get database path from environment variable or config (env var takes precedence)
        var databasePath = Environment.GetEnvironmentVariable("DATABASE_PATH")
            ?? configuration.GetValue<string>("Database:Path")
            ?? "tldr.sqlite";
        var connectionString = $"Data Source={databasePath}";

        Client = new DiscordSocketClient(new DiscordSocketConfig
        {
            // Explicitly specify intents - GuildMembers is enabled in portal so include it
            GatewayIntents = GatewayIntents.Guilds
                | GatewayIntents.GuildMembers  // Privileged - enabled in Discord portal
                | GatewayIntents.GuildMessages
                | GatewayIntents.GuildMessageReactions
                | GatewayIntents.DirectMessages
                | GatewayIntents.DirectMessageReactions
                | GatewayIntents.MessageContent,  // Privileged - enabled in Discord portal
            // Log gateway intent issues
            LogGatewayIntentWarnings = true,
            // Ensure we receive all messages
            MessageCacheSize = 100,
            LogLevel = LogSeverity.Debug
        });

        Interactions = new InteractionService(Client.Rest);

        Collector = new MessageCollectorService();
        Logger = new SummaryLogger(Collector);

        var openAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(openAiKey))
        {
            throw new InvalidOperationException("Missing OPENAI_API_KEY in environment variables.");
        }

        _services = new ServiceCollection()
            .AddLogging(config => {
                config.ClearProviders();
                config.AddNLog();
            })
            // Add configuration
            .AddSingleton<IConfiguration>(configuration)
            // Bind configuration sections to strongly-typed objects
            .Configure<OpenAISettings>(configuration.GetSection("OpenAI"))
            .Configure<CacheSettings>(configuration.GetSection("Cache"))
            .Configure<RateLimitsSettings>(configuration.GetSection("RateLimits"))
            .Configure<DiscordSettings>(configuration.GetSection("Discord"))
            .Configure<CleanupSettings>(configuration.GetSection("Cleanup"))
            .AddDbContextFactory<TldrDbContext>(options =>
                options.UseSqlite(connectionString))
            .AddSingleton<GuildAccessService>()
            .AddSingleton<GuildSettingsService>()
            .AddSingleton(Collector)
            .AddSingleton(Logger)
            .AddSingleton<CostTrackerService>()
            .AddSingleton<IOpenAIService>(new OpenAIService(new OpenAiOptions { ApiKey = openAiKey }))
            .AddSingleton<AiSummarizerService>()
            .AddSingleton<SummaryCacheService>()
            .AddSingleton(Client)
            .AddSingleton(Interactions)
            .AddSingleton<SuperuserService>()
            .AddSingleton<MessageCommandHandler>()
            .AddSingleton<SpamBlockerService>()
            .BuildServiceProvider();

        // Get logger instance
        _logger = _services.GetRequiredService<ILogger<Startup>>();

        // Run database migrations using the factory
        var dbFactory = _services.GetRequiredService<IDbContextFactory<TldrDbContext>>();
        await using (var db = await dbFactory.CreateDbContextAsync())
        {
            // Clear any stale migration locks from previous crashed deployments
            // This is safe for single-instance deployments (like Railway)
            try
            {
                await db.Database.ExecuteSqlRawAsync(
                    "DELETE FROM \"__EFMigrationsLock\" WHERE 1=1");
                _logger?.LogInformation("Cleared stale migration locks");
            }
            catch
            {
                // Table might not exist yet on first run - that's fine
            }

            await db.Database.MigrateAsync();
        }

        await Interactions.AddModulesAsync(typeof(Startup).Assembly, _services);

        if (!_eventsRegistered)
        {
            Client.Log += Log;
            Client.MessageReceived += MessageReceived;
            Console.WriteLine("✅ MessageReceived event handler registered");

            Client.InteractionCreated += async interaction =>
            {
                var ctx = new SocketInteractionContext(Client, interaction);
                var result = await Interactions.ExecuteCommandAsync(ctx, _services);
                if (!result.IsSuccess)
                {
                    var logger = _services.GetRequiredService<ILogger<Startup>>();
                    logger.LogError("[TLDrkseid] Command failed: {Error} - {ErrorReason}", result.Error, result.ErrorReason);
                }
            };

            Client.Ready += async () =>
            {
                var logger = _services.GetRequiredService<ILogger<Startup>>();

                // Log the actual intents the client is using
                var config = Client.CurrentUser != null ? "Connected" : "Not connected";
                logger.LogInformation("🔧 Gateway Intents requested: {Intents} (value: {IntentValue})",
                    Client.CurrentUser != null ? "Ready" : "Unknown",
                    (int)(GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.GuildMessageReactions
                        | GatewayIntents.DirectMessages | GatewayIntents.DirectMessageReactions | GatewayIntents.MessageContent));

                var devGuildId = Environment.GetEnvironmentVariable("DISCORD_DEV_GUILD_ID");
                if (ulong.TryParse(devGuildId, out var guildId))
                {
                    await Interactions.RegisterCommandsToGuildAsync(guildId, true);
                    logger.LogInformation("✅ TLDrkseid slash commands registered to dev guild: {GuildId}", guildId);
                }
                else
                {
                    await Interactions.RegisterCommandsGloballyAsync(true);
                    logger.LogInformation("✅ TLDrkseid slash commands registered globally.");
                }
            };

            _eventsRegistered = true;
        }
    }

    public async Task StartAsync(string token)
    {
        await Client.LoginAsync(TokenType.Bot, token);
        await Client.StartAsync();
        StartTimers();
    }

    public async Task StopAsync()
    {
        // Dispose timers
        _hourlyTimer?.Dispose();
        _dailyTimer?.Dispose();

        // Unsubscribe events
        Client.Log -= Log;
        Client.MessageReceived -= MessageReceived;

        // Dispose async services (await pending tasks)
        var messageHandler = _services.GetService<MessageCommandHandler>();
        if (messageHandler != null)
        {
            await messageHandler.DisposeAsync();
        }

        // Stop and cleanup the Discord client
        if (Client != null)
        {
            await Client.StopAsync();
            await Client.LogoutAsync();
            Client.Dispose();
        }

        if (_services is IDisposable disposableServices)
        {
            disposableServices.Dispose();
        }
    }

    private void StartTimers()
    {
        _hourlyTimer = new Timer(async _ => await Logger.LogSummary("hourly"), null, GetInitialDelay(TimeSpan.FromHours(1)), TimeSpan.FromHours(1));
        _dailyTimer = new Timer(async _ => await Logger.LogSummary("daily"), null, GetInitialDelay(TimeSpan.FromDays(1)), TimeSpan.FromDays(1));
    }

    private TimeSpan GetInitialDelay(TimeSpan interval)
    {
        var now = DateTime.UtcNow;
        var next = now.Add(interval);
        var delay = next - now;
        return TimeSpan.FromSeconds(delay.TotalSeconds % interval.TotalSeconds);
    }

    private async Task MessageReceived(SocketMessage message)
    {
        try
        {
            // Debug: Use Console.WriteLine as fallback in case logging isn't working
            var logMsg = $"[MessageReceived] From: {message.Author?.Username ?? "null"}, Channel: {message.Channel?.Name ?? "null"}, Content length: {message.Content?.Length ?? -1}";
            Console.WriteLine(logMsg);
            _logger?.LogInformation(logMsg);

            if (message.Author.IsBot || message.Channel is not SocketTextChannel) return;

            Collector.Track(message);
            var handler = _services.GetRequiredService<MessageCommandHandler>();
            await handler.HandleAsync(message);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MessageReceived ERROR] {ex.GetType().Name}: {ex.Message}");
            _logger?.LogError(ex, "[MessageReceived] Exception in handler");
        }
    }

    private Task Log(LogMessage msg)
    {
        var logLevel = msg.Severity switch
        {
            LogSeverity.Critical => Microsoft.Extensions.Logging.LogLevel.Critical,
            LogSeverity.Error => Microsoft.Extensions.Logging.LogLevel.Error,
            LogSeverity.Warning => Microsoft.Extensions.Logging.LogLevel.Warning,
            LogSeverity.Info => Microsoft.Extensions.Logging.LogLevel.Information,
            LogSeverity.Verbose => Microsoft.Extensions.Logging.LogLevel.Debug,
            LogSeverity.Debug => Microsoft.Extensions.Logging.LogLevel.Trace,
            _ => Microsoft.Extensions.Logging.LogLevel.Information
        };

        _logger?.Log(logLevel, msg.Exception, "[Discord] {Source}: {Message}", msg.Source, msg.Message);
        return Task.CompletedTask;
    }
}
