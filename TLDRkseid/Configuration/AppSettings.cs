namespace TLDRkseid.Configuration;

public class DatabaseSettings
{
    public string Path { get; set; } = "tldr.sqlite";
}

public class OpenAISettings
{
    public string Model { get; set; } = "gpt-3.5-turbo";
    public int MaxTokens { get; set; } = 300;
    public float Temperature { get; set; } = 0.7f;
    public int TimeoutSeconds { get; set; } = 30;
    public double CostPerThousandTokens { get; set; } = 0.002;
    public int DebounceMilliseconds { get; set; } = 2000;
}

public class CacheSettings
{
    public int MaxEntries { get; set; } = 1000;
    public int TtlMinutes { get; set; } = 60;
}

public class RateLimitSettings
{
    public int CooldownSeconds { get; set; }
    public int BurstWindowSeconds { get; set; }
    public int BurstThreshold { get; set; }
}

public class RateLimitsSettings
{
    public RateLimitSettings Summarization { get; set; } = new();
    public RateLimitSettings CachedResponse { get; set; } = new();
    public RateLimitSettings AdminCommand { get; set; } = new();
    public RateLimitSettings DatabaseWrite { get; set; } = new();
}

public class DiscordSettings
{
    public int MessageFetchTimeoutSeconds { get; set; } = 15;
    public int MaxRetries { get; set; } = 3;
    public int InitialRetryDelaySeconds { get; set; } = 1;
    public int EmbedDescriptionLimit { get; set; } = 4096;
}

public class CleanupSettings
{
    public int IntervalMinutes { get; set; } = 5;
    public int EntryExpirationMinutes { get; set; } = 10;
}
