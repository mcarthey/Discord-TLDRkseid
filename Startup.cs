using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordPA.Commands;
using DiscordPA.Data;
using DiscordPA.Handlers;
using DiscordPA.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging; // Add logging namespace

namespace DiscordPA;

public class Startup
{
    public DiscordSocketClient Client { get; private set; } = default!;
    public InteractionService Interactions { get; private set; } = default!;
    public MessageCollectorService Collector { get; private set; } = default!;
    public SummaryLogger Logger { get; private set; } = default!;
    private IServiceProvider _services = default!;
    private bool _eventsRegistered = false;

    public async Task InitializeAsync()
    {
        var db = new TldrDbContext();
        db.Database.Migrate();

        Client = new DiscordSocketClient(new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent
        });

        Interactions = new InteractionService(Client.Rest);

        Collector = new MessageCollectorService();
        Logger = new SummaryLogger(Collector);

        var openAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        var costTracker = new CostTrackerService();
        var aiSummarizer = new AiSummarizerService(openAiKey, costTracker);

        _services = new ServiceCollection()
            .AddLogging(config => {
                config.ClearProviders();
                config.AddNLog(); // Assumes NLog.Extensions.Logging is installed
            })
            .AddSingleton(db)
            .AddSingleton<GuildAccessService>()
            .AddSingleton(Collector)
            .AddSingleton(Logger)
            .AddSingleton(costTracker)
            .AddSingleton(aiSummarizer)
            .AddSingleton<SummaryCacheService>()
            .AddSingleton(Client)
            .AddSingleton(Interactions)
            .AddSingleton<SuperuserService>()
            .AddSingleton<MessageCommandHandler>()
            .AddSingleton<SpamBlockerService>()
            .BuildServiceProvider();

        await Interactions.AddModulesAsync(typeof(Startup).Assembly, _services);

        if (!_eventsRegistered)
        {
            Client.Log += Log;
            Client.MessageReceived += MessageReceived;

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
                var devGuildId = Environment.GetEnvironmentVariable("DISCORD_DEV_GUILD_ID");
                if (ulong.TryParse(devGuildId, out var guildId))
                {
                    await Interactions.RegisterCommandsToGuildAsync(guildId, true);
                    var logger = _services.GetRequiredService<ILogger<Startup>>();
                    logger.LogInformation("✅ TLDrkseid slash commands registered to dev guild: {GuildId}", guildId);
                }
                else
                {
                    await Interactions.RegisterCommandsGloballyAsync(true);
                    var logger = _services.GetRequiredService<ILogger<Startup>>();
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
        // Unsubscribe events
        Client.Log -= Log;
        Client.MessageReceived -= MessageReceived;
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
        var hourly = new Timer(async _ => await Logger.LogSummary("hourly"), null, GetInitialDelay(TimeSpan.FromHours(1)), TimeSpan.FromHours(1));
        var daily = new Timer(async _ => await Logger.LogSummary("daily"), null, GetInitialDelay(TimeSpan.FromDays(1)), TimeSpan.FromDays(1));
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
        if (message.Author.IsBot || message.Channel is not SocketTextChannel) return;

        Collector.Track(message);
        var handler = _services.GetRequiredService<MessageCommandHandler>();
        await handler.HandleAsync(message);
    }

    private Task Log(LogMessage msg)
    {
        Console.WriteLine(msg.ToString());
        return Task.CompletedTask;
    }
}
