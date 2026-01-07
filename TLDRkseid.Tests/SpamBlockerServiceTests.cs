using TLDRkseid.Services;

namespace TLDRkseid.Tests;

public class SpamBlockerServiceTests : IDisposable
{
    private readonly SpamBlockerService _spamBlocker;

    public SpamBlockerServiceTests()
    {
        _spamBlocker = new SpamBlockerService();
    }

    public void Dispose()
    {
        _spamBlocker.Dispose();
    }

    [Fact]
    public void IsSpamming_Returns_False_For_First_Request()
    {
        // Act
        var isSpamming = _spamBlocker.IsSpamming("guild1", "channel1", "user1", isAdmin: false, wasCached: false, out var reason);

        // Assert
        Assert.False(isSpamming);
        Assert.Empty(reason);
    }

    [Fact]
    public void IsSpamming_Returns_False_For_Admins()
    {
        // Act - even rapid requests should pass for admins
        _spamBlocker.IsSpamming("guild1", "channel1", "admin1", isAdmin: true, wasCached: false, out _);
        var isSpamming = _spamBlocker.IsSpamming("guild1", "channel1", "admin1", isAdmin: true, wasCached: false, out var reason);

        // Assert
        Assert.False(isSpamming);
        Assert.Empty(reason);
    }

    [Fact]
    public void IsSpamming_Returns_False_For_Cached_Responses()
    {
        // Act - cached responses bypass spam check
        _spamBlocker.IsSpamming("guild1", "channel1", "user1", isAdmin: false, wasCached: true, out _);
        var isSpamming = _spamBlocker.IsSpamming("guild1", "channel1", "user1", isAdmin: false, wasCached: true, out var reason);

        // Assert
        Assert.False(isSpamming);
        Assert.Empty(reason);
    }

    [Fact]
    public void IsSpamming_Returns_True_For_Rapid_Requests()
    {
        // Arrange - make first request
        _spamBlocker.IsSpamming("guild1", "channel1", "user1", isAdmin: false, wasCached: false, out _);

        // Act - immediate second request should be blocked (cooldown)
        var isSpamming = _spamBlocker.IsSpamming("guild1", "channel1", "user1", isAdmin: false, wasCached: false, out var reason);

        // Assert
        Assert.True(isSpamming);
        Assert.Contains("Cooldown active", reason);
    }

    [Fact]
    public void IsCachedSpamming_Returns_False_For_First_Request()
    {
        // Act
        var isSpamming = _spamBlocker.IsCachedSpamming("guild1", "channel1", "user1", out var reason);

        // Assert
        Assert.False(isSpamming);
        Assert.Empty(reason);
    }

    [Fact]
    public void IsCachedSpamming_Returns_True_For_Rapid_Requests()
    {
        // Arrange - first request
        _spamBlocker.IsCachedSpamming("guild1", "channel1", "user1", out _);

        // Act - immediate second request
        var isSpamming = _spamBlocker.IsCachedSpamming("guild1", "channel1", "user1", out var reason);

        // Assert
        Assert.True(isSpamming);
        Assert.Contains("Cooldown active", reason);
    }

    [Fact]
    public void IsAdminCommandSpamming_Returns_False_For_First_Command()
    {
        // Act
        var isSpamming = _spamBlocker.IsAdminCommandSpamming("guild1", "user1", out var reason);

        // Assert
        Assert.False(isSpamming);
        Assert.Empty(reason);
    }

    [Fact]
    public void IsDatabaseWriteRateLimited_Returns_False_For_First_Write()
    {
        // Act
        var isLimited = _spamBlocker.IsDatabaseWriteRateLimited(12345UL, out var reason);

        // Assert
        Assert.False(isLimited);
        Assert.Empty(reason);
    }

    [Fact]
    public void IsDatabaseWriteRateLimited_Returns_True_For_Rapid_Writes()
    {
        // Arrange - first write
        _spamBlocker.IsDatabaseWriteRateLimited(12345UL, out _);

        // Act - immediate second write
        var isLimited = _spamBlocker.IsDatabaseWriteRateLimited(12345UL, out var reason);

        // Assert
        Assert.True(isLimited);
        Assert.Contains("Rate limited", reason);
    }

    [Fact]
    public void Different_Guilds_Have_Separate_Rate_Limits()
    {
        // Arrange - write to guild1
        _spamBlocker.IsDatabaseWriteRateLimited(11111UL, out _);

        // Act - write to different guild should pass
        var isLimited = _spamBlocker.IsDatabaseWriteRateLimited(22222UL, out var reason);

        // Assert
        Assert.False(isLimited);
        Assert.Empty(reason);
    }
}
