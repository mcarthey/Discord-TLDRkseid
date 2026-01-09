using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace TLDRkseid.Services.HealthChecks;

public class OpenAIHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");

        var data = new Dictionary<string, object>
        {
            { "KeyConfigured", !string.IsNullOrWhiteSpace(apiKey) },
            { "KeyPrefix", !string.IsNullOrWhiteSpace(apiKey) ? apiKey[..Math.Min(7, apiKey.Length)] + "..." : "N/A" }
        };

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "OPENAI_API_KEY not configured",
                data: data));
        }

        if (!apiKey.StartsWith("sk-"))
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                "OPENAI_API_KEY format may be invalid (expected 'sk-' prefix)",
                data: data));
        }

        return Task.FromResult(HealthCheckResult.Healthy(
            "OpenAI API key configured",
            data));
    }
}
