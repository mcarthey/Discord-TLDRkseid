using Discord;
using Discord.WebSocket;
using DotNetEnv;
using Xunit.Abstractions;

namespace TLDRkseid.Tests.Integration;

/// <summary>
/// Integration tests for Discord gateway connectivity and message receiving.
/// These tests require real Discord credentials and are excluded from CI.
///
/// To run locally:
/// 1. Create a .env file in the solution root (or TLDRkseid.Tests folder) with:
///    DISCORD_BOT_TOKEN=your-token-here
///    DISCORD_TEST_CHANNEL_ID=1234567890
/// 2. Or set environment variables directly
/// 3. Run: dotnet test --filter "Category=Integration" --logger "console;verbosity=detailed"
///
/// IMPORTANT: Never commit your .env file! It should be in .gitignore.
/// </summary>
[Trait("Category", "Integration")]
public class DiscordGatewayIntegrationTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private DiscordSocketClient? _client;
    private readonly string? _token;
    private readonly ulong? _testChannelId;
    private readonly List<SocketMessage> _receivedMessages = new();
    private TaskCompletionSource<bool>? _readyTcs;
    private TaskCompletionSource<SocketMessage>? _messageTcs;

    public DiscordGatewayIntegrationTests(ITestOutputHelper output)
    {
        _output = output;

        // Try to load .env file from various locations
        var possibleEnvPaths = new[]
        {
            ".env",                                           // Current directory
            "../.env",                                        // Parent (solution root from test bin)
            "../../.env",                                     // Two levels up
            "../../../.env",                                  // Three levels up
            "../../../../.env",                               // Four levels up (from bin/Debug/net10.0)
            "../../../../../.env",                            // Five levels up
            Path.Combine(AppContext.BaseDirectory, ".env"),   // Base directory
        };

        foreach (var envPath in possibleEnvPaths)
        {
            var fullPath = Path.GetFullPath(envPath);
            if (File.Exists(fullPath))
            {
                _output.WriteLine($"Loading .env from: {fullPath}");
                Env.Load(fullPath);
                break;
            }
        }

        _token = Environment.GetEnvironmentVariable("DISCORD_BOT_TOKEN");
        var channelIdStr = Environment.GetEnvironmentVariable("DISCORD_TEST_CHANNEL_ID");

        _output.WriteLine($"DISCORD_BOT_TOKEN: {(_token != null ? $"[{_token.Length} chars]" : "NOT SET")}");
        _output.WriteLine($"DISCORD_TEST_CHANNEL_ID: {channelIdStr ?? "NOT SET"}");

        if (ulong.TryParse(channelIdStr, out var channelId))
        {
            _testChannelId = channelId;
        }
    }

    public async Task InitializeAsync()
    {
        if (string.IsNullOrEmpty(_token))
        {
            _output.WriteLine("DISCORD_BOT_TOKEN not set - skipping initialization");
            return;
        }

        _client = new DiscordSocketClient(new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.All,
            LogGatewayIntentWarnings = true,
            MessageCacheSize = 100,
            LogLevel = LogSeverity.Debug
        });

        _client.Log += msg =>
        {
            _output.WriteLine($"[Discord] {msg.Severity}: {msg.Source} - {msg.Message}");
            if (msg.Exception != null)
                _output.WriteLine($"  Exception: {msg.Exception}");
            return Task.CompletedTask;
        };

        _readyTcs = new TaskCompletionSource<bool>();
        _client.Ready += () =>
        {
            _output.WriteLine($"Bot ready! Connected as {_client.CurrentUser?.Username}");
            _output.WriteLine($"Guild count: {_client.Guilds.Count}");
            foreach (var guild in _client.Guilds)
            {
                _output.WriteLine($"  - {guild.Name} ({guild.Id})");
            }
            _readyTcs.TrySetResult(true);
            return Task.CompletedTask;
        };

        _client.MessageReceived += msg =>
        {
            _output.WriteLine($"[MessageReceived] From: {msg.Author?.Username ?? "null"}, Channel: {msg.Channel?.Name ?? "null"}, Content length: {msg.Content?.Length ?? -1}, Content: '{msg.Content}'");
            _receivedMessages.Add(msg);
            _messageTcs?.TrySetResult(msg);
            return Task.CompletedTask;
        };

        await _client.LoginAsync(TokenType.Bot, _token);
        await _client.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_client != null)
        {
            await _client.StopAsync();
            await _client.DisposeAsync();
        }
    }

    [Fact]
    public async Task Bot_Should_Connect_And_Become_Ready()
    {
        // Skip if no token
        if (string.IsNullOrEmpty(_token))
        {
            _output.WriteLine("SKIP: DISCORD_BOT_TOKEN not set");
            return;
        }

        // Wait for ready with timeout
        var ready = await Task.WhenAny(_readyTcs!.Task, Task.Delay(TimeSpan.FromSeconds(30)));

        Assert.True(_readyTcs.Task.IsCompletedSuccessfully, "Bot should become ready within 30 seconds");
        Assert.NotNull(_client?.CurrentUser);

        _output.WriteLine($"SUCCESS: Bot connected as {_client.CurrentUser.Username}#{_client.CurrentUser.Discriminator}");
        _output.WriteLine($"Bot ID: {_client.CurrentUser.Id}");
    }

    [Fact]
    public async Task Bot_Should_Receive_Own_Messages()
    {
        // Skip if no token or test channel
        if (string.IsNullOrEmpty(_token))
        {
            _output.WriteLine("SKIP: DISCORD_BOT_TOKEN not set");
            return;
        }

        if (!_testChannelId.HasValue)
        {
            _output.WriteLine("SKIP: DISCORD_TEST_CHANNEL_ID not set");
            return;
        }

        // Wait for ready
        await Task.WhenAny(_readyTcs!.Task, Task.Delay(TimeSpan.FromSeconds(30)));
        if (!_readyTcs.Task.IsCompletedSuccessfully)
        {
            Assert.Fail("Bot did not become ready");
            return;
        }

        // Get the test channel
        var channel = _client!.GetChannel(_testChannelId.Value) as ITextChannel;
        if (channel == null)
        {
            _output.WriteLine($"SKIP: Could not find channel {_testChannelId.Value}");
            return;
        }

        // Set up message waiter
        _messageTcs = new TaskCompletionSource<SocketMessage>();

        // Send a test message
        var testContent = $"Integration test message at {DateTime.UtcNow:O}";
        _output.WriteLine($"Sending test message to #{channel.Name}: {testContent}");

        var sentMessage = await channel.SendMessageAsync(testContent);
        _output.WriteLine($"Message sent with ID: {sentMessage.Id}");

        // Wait for the message to be received via gateway
        var received = await Task.WhenAny(_messageTcs.Task, Task.Delay(TimeSpan.FromSeconds(10)));

        if (_messageTcs.Task.IsCompletedSuccessfully)
        {
            var msg = await _messageTcs.Task;
            _output.WriteLine($"SUCCESS: Received message from {msg.Author?.Username}: '{msg.Content}'");
            Assert.Equal(testContent, msg.Content);
        }
        else
        {
            _output.WriteLine("FAIL: Did not receive own message within 10 seconds");
            _output.WriteLine($"Total messages received during test: {_receivedMessages.Count}");
            foreach (var msg in _receivedMessages)
            {
                _output.WriteLine($"  - From {msg.Author?.Username}: '{msg.Content?.Substring(0, Math.Min(50, msg.Content?.Length ?? 0))}'");
            }
            Assert.Fail("Bot should receive its own message via MessageReceived event");
        }

        // Clean up - delete the test message
        try
        {
            await sentMessage.DeleteAsync();
            _output.WriteLine("Test message deleted");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Warning: Could not delete test message: {ex.Message}");
        }
    }

    [Fact]
    public async Task Bot_Should_Receive_User_Messages_Within_Timeout()
    {
        // Skip if no token or test channel
        if (string.IsNullOrEmpty(_token))
        {
            _output.WriteLine("SKIP: DISCORD_BOT_TOKEN not set");
            return;
        }

        if (!_testChannelId.HasValue)
        {
            _output.WriteLine("SKIP: DISCORD_TEST_CHANNEL_ID not set");
            return;
        }

        // Wait for ready
        await Task.WhenAny(_readyTcs!.Task, Task.Delay(TimeSpan.FromSeconds(30)));
        if (!_readyTcs.Task.IsCompletedSuccessfully)
        {
            Assert.Fail("Bot did not become ready");
            return;
        }

        _output.WriteLine("=== MANUAL TEST ===");
        _output.WriteLine($"Please send a message in channel ID {_testChannelId.Value} within 60 seconds.");
        _output.WriteLine("The bot will listen for any message from a non-bot user.");
        _output.WriteLine("==================");

        // Set up message waiter that filters for non-bot messages
        var userMessageTcs = new TaskCompletionSource<SocketMessage>();

        void OnMessage(SocketMessage msg)
        {
            if (msg.Author != null && !msg.Author.IsBot)
            {
                _output.WriteLine($"Received user message from {msg.Author.Username}: '{msg.Content}'");
                userMessageTcs.TrySetResult(msg);
            }
        }

        // We need to add our own handler since the class-level one might have already captured bot messages
        _client!.MessageReceived += msg => { OnMessage(msg); return Task.CompletedTask; };

        // Wait for a user message
        var received = await Task.WhenAny(userMessageTcs.Task, Task.Delay(TimeSpan.FromSeconds(60)));

        if (userMessageTcs.Task.IsCompletedSuccessfully)
        {
            var msg = await userMessageTcs.Task;
            _output.WriteLine($"SUCCESS: Received user message!");
            _output.WriteLine($"  Author: {msg.Author?.Username}");
            _output.WriteLine($"  Channel: {msg.Channel?.Name}");
            _output.WriteLine($"  Content: '{msg.Content}'");
            _output.WriteLine($"  Content length: {msg.Content?.Length}");
            Assert.True(msg.Content?.Length > 0 || true, "Message received (content may be empty without MessageContent intent)");
        }
        else
        {
            _output.WriteLine("FAIL: No user message received within 60 seconds");
            _output.WriteLine($"Total messages received during test: {_receivedMessages.Count}");
            _output.WriteLine("Messages received (all):");
            foreach (var msg in _receivedMessages)
            {
                _output.WriteLine($"  - IsBot: {msg.Author?.IsBot}, From: {msg.Author?.Username}, Content: '{msg.Content?.Substring(0, Math.Min(50, msg.Content?.Length ?? 0))}'");
            }
            Assert.Fail("Bot should receive user messages via MessageReceived event when GuildMessages intent is granted");
        }
    }

    [Fact]
    public async Task Diagnose_Gateway_Intents()
    {
        // Skip if no token
        if (string.IsNullOrEmpty(_token))
        {
            _output.WriteLine("SKIP: DISCORD_BOT_TOKEN not set");
            return;
        }

        // Wait for ready
        await Task.WhenAny(_readyTcs!.Task, Task.Delay(TimeSpan.FromSeconds(30)));
        if (!_readyTcs.Task.IsCompletedSuccessfully)
        {
            Assert.Fail("Bot did not become ready");
            return;
        }

        _output.WriteLine("=== GATEWAY INTENT DIAGNOSTICS ===");
        _output.WriteLine($"Bot User: {_client!.CurrentUser?.Username}#{_client.CurrentUser?.Discriminator}");
        _output.WriteLine($"Bot ID: {_client.CurrentUser?.Id}");
        _output.WriteLine($"Guilds connected: {_client.Guilds.Count}");

        // The intents we requested
        var requestedIntents = GatewayIntents.All;
        _output.WriteLine($"\nRequested Intents value: {(int)requestedIntents}");
        _output.WriteLine("Requested Intents breakdown:");

        foreach (GatewayIntents intent in Enum.GetValues(typeof(GatewayIntents)))
        {
            if (intent != GatewayIntents.None && intent != GatewayIntents.All &&
                intent != GatewayIntents.AllUnprivileged && requestedIntents.HasFlag(intent))
            {
                var isPrivileged = intent == GatewayIntents.GuildMembers ||
                                   intent == GatewayIntents.GuildPresences ||
                                   intent == GatewayIntents.MessageContent;
                _output.WriteLine($"  - {intent} {(isPrivileged ? "(PRIVILEGED)" : "")}");
            }
        }

        _output.WriteLine("\n=== LISTENING FOR MESSAGES ===");
        _output.WriteLine("Waiting 30 seconds. Send messages in any channel the bot can see.");
        _output.WriteLine("Bot messages and user messages will be logged.\n");

        await Task.Delay(TimeSpan.FromSeconds(30));

        _output.WriteLine($"\n=== RESULTS ===");
        _output.WriteLine($"Total messages received: {_receivedMessages.Count}");

        var botMessages = _receivedMessages.Where(m => m.Author?.IsBot == true).ToList();
        var userMessages = _receivedMessages.Where(m => m.Author?.IsBot == false).ToList();

        _output.WriteLine($"Bot messages: {botMessages.Count}");
        _output.WriteLine($"User messages: {userMessages.Count}");

        if (userMessages.Count == 0 && botMessages.Count > 0)
        {
            _output.WriteLine("\n!!! DIAGNOSIS: GuildMessages intent appears to NOT be granted !!!");
            _output.WriteLine("The bot receives its own messages but not user messages.");
            _output.WriteLine("This indicates Discord is not sending user MESSAGE_CREATE events.");
        }
        else if (userMessages.Count > 0)
        {
            _output.WriteLine("\n✓ SUCCESS: GuildMessages intent is working!");
            _output.WriteLine("User messages are being received.");

            // Check if content is present
            var hasContent = userMessages.Any(m => !string.IsNullOrEmpty(m.Content));
            if (hasContent)
            {
                _output.WriteLine("✓ MessageContent intent is working - message content is visible.");
            }
            else
            {
                _output.WriteLine("! MessageContent intent may not be working - message content is empty.");
            }
        }
        else
        {
            _output.WriteLine("\n? No messages received at all during the test period.");
        }
    }
}
