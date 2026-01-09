using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace TLDRkseid.Services.HealthChecks;

public class DiscordHealthCheck : IHealthCheck
{
    private readonly DiscordSocketClient _client;

    public DiscordHealthCheck(DiscordSocketClient client)
    {
        _client = client;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object>
        {
            { "ConnectionState", _client.ConnectionState.ToString() },
            { "LoginState", _client.LoginState.ToString() },
            { "Latency", _client.Latency },
            { "GuildCount", _client.Guilds.Count }
        };

        if (_client.ConnectionState == ConnectionState.Connected &&
            _client.LoginState == LoginState.LoggedIn)
        {
            return Task.FromResult(HealthCheckResult.Healthy(
                $"Discord connected. Latency: {_client.Latency}ms, Guilds: {_client.Guilds.Count}",
                data));
        }

        if (_client.ConnectionState == ConnectionState.Connecting)
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                "Discord is connecting...",
                data: data));
        }

        return Task.FromResult(HealthCheckResult.Unhealthy(
            $"Discord not connected. State: {_client.ConnectionState}",
            data: data));
    }
}
