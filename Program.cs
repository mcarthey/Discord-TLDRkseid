using DotNetEnv;
using System.Runtime.Loader;

namespace DiscordPA;

class Program
{
    static async Task Main(string[] args)
    {
        Env.Load(); // Loads from .env by default

        var startup = new Startup();
        await startup.InitializeAsync();

        var token = Environment.GetEnvironmentVariable("DISCORD_BOT_TOKEN");
        if (string.IsNullOrWhiteSpace(token))
        {
            Console.WriteLine("Missing DISCORD_BOT_TOKEN in .env file.");
            return;
        }

        await startup.StartAsync(token);

        // Handle shutdown signals
        var waitForStop = new TaskCompletionSource<bool>();

        AssemblyLoadContext.Default.Unloading += _ =>
        {
            waitForStop.SetResult(true);
        };

        Console.CancelKeyPress += (sender, eventArgs) =>
        {
            eventArgs.Cancel = true;
            waitForStop.SetResult(true);
        };

        await waitForStop.Task;
        await startup.StopAsync();
    }
}
