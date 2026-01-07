using TLDRkseid.Services;

namespace TLDRkseid.Tests;

public class SummaryCacheServiceTests
{
    [Fact]
    public void Set_And_TryGet_Returns_Cached_Summary()
    {
        // Arrange
        var cache = new SummaryCacheService();
        var messages = new List<string> { "user1: hello", "user2: hi there" };
        var summary = "A greeting exchange between users.";
        var cost = 0.001;

        // Act
        cache.Set("guild1", "channel1", "standard", null, messages, summary, cost);
        var found = cache.TryGet("guild1", "channel1", "standard", null, messages, out var retrievedSummary, out var retrievedCost);

        // Assert
        Assert.True(found);
        Assert.Equal(summary, retrievedSummary);
        Assert.Equal(cost, retrievedCost);
    }

    [Fact]
    public void TryGet_Returns_False_For_Missing_Cache()
    {
        // Arrange
        var cache = new SummaryCacheService();
        var messages = new List<string> { "user1: hello" };

        // Act
        var found = cache.TryGet("guild1", "channel1", "standard", null, messages, out var summary, out var cost);

        // Assert
        Assert.False(found);
        Assert.Equal(string.Empty, summary);
        Assert.Equal(0, cost);
    }

    [Fact]
    public void TryGet_Returns_False_When_Messages_Changed()
    {
        // Arrange
        var cache = new SummaryCacheService();
        var originalMessages = new List<string> { "user1: hello" };
        var newMessages = new List<string> { "user1: hello", "user2: new message" };

        cache.Set("guild1", "channel1", "standard", null, originalMessages, "summary", 0.001);

        // Act
        var found = cache.TryGet("guild1", "channel1", "standard", null, newMessages, out _, out _);

        // Assert
        Assert.False(found);
    }

    [Fact]
    public void TryGet_Trickles_Down_From_Deeper_Cached_Tier()
    {
        // Arrange
        var cache = new SummaryCacheService();
        var messages = new List<string> { "user1: discussion content" };
        var deepSummary = "Deep summary content";

        // Cache at "deep" tier
        cache.Set("guild1", "channel1", "deep", null, messages, deepSummary, 0.002);

        // Act - request "standard" tier (shallower than deep)
        var found = cache.TryGet("guild1", "channel1", "standard", null, messages, out var summary, out _);

        // Assert
        Assert.True(found);
        Assert.Contains("Cached from `deep` tier", summary);
        Assert.Contains(deepSummary, summary);
    }

    [Fact]
    public void Cache_Separates_By_User_Filter()
    {
        // Arrange
        var cache = new SummaryCacheService();
        var messages = new List<string> { "user1: hello" };

        cache.Set("guild1", "channel1", "standard", "user123", messages, "filtered summary", 0.001);

        // Act - request without user filter should miss
        var foundAll = cache.TryGet("guild1", "channel1", "standard", null, messages, out _, out _);
        var foundFiltered = cache.TryGet("guild1", "channel1", "standard", "user123", messages, out var summary, out _);

        // Assert
        Assert.False(foundAll);
        Assert.True(foundFiltered);
        Assert.Equal("filtered summary", summary);
    }
}
