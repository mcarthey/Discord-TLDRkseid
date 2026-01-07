using Microsoft.EntityFrameworkCore;
using Moq;
using TLDRkseid.Data;
using TLDRkseid.Services;

namespace TLDRkseid.Tests;

public class GuildSettingsServiceTests : IDisposable
{
    private readonly TldrDbContext _context;
    private readonly IDbContextFactory<TldrDbContext> _factory;
    private readonly SpamBlockerService _spamBlocker;
    private readonly GuildSettingsService _service;

    public GuildSettingsServiceTests()
    {
        var options = new DbContextOptionsBuilder<TldrDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new TldrDbContext(options);

        var factoryMock = new Mock<IDbContextFactory<TldrDbContext>>();
        factoryMock.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new TldrDbContext(options));
        _factory = factoryMock.Object;

        _spamBlocker = new SpamBlockerService();
        _service = new GuildSettingsService(_factory, _spamBlocker);
    }

    public void Dispose()
    {
        _context.Dispose();
        _spamBlocker.Dispose();
    }

    [Fact]
    public async Task GetPreferredDepthAsync_Returns_Default_For_New_Guild()
    {
        // Act
        var depth = await _service.GetPreferredDepthAsync(12345UL);

        // Assert
        Assert.Equal(GuildSettingsService.DefaultDepth, depth);
    }

    [Fact]
    public async Task SetPreferredDepthAsync_Persists_Depth()
    {
        // Arrange
        var guildId = 12345UL;

        // Act
        var success = await _service.SetPreferredDepthAsync(guildId, "deep");
        var depth = await _service.GetPreferredDepthAsync(guildId);

        // Assert
        Assert.True(success);
        Assert.Equal("deep", depth);
    }

    [Fact]
    public async Task SetPreferredDepthAsync_Returns_False_For_Invalid_Depth()
    {
        // Act
        var success = await _service.SetPreferredDepthAsync(12345UL, "invalid");

        // Assert
        Assert.False(success);
    }

    [Theory]
    [InlineData("recent")]
    [InlineData("brief")]
    [InlineData("standard")]
    [InlineData("deep")]
    [InlineData("max")]
    public void IsValidDepth_Returns_True_For_Valid_Depths(string depth)
    {
        // Act & Assert
        Assert.True(GuildSettingsService.IsValidDepth(depth));
    }

    [Theory]
    [InlineData("RECENT")]
    [InlineData("Brief")]
    [InlineData("STANDARD")]
    public void IsValidDepth_Is_Case_Insensitive(string depth)
    {
        // Act & Assert
        Assert.True(GuildSettingsService.IsValidDepth(depth));
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("")]
    [InlineData("shallow")]
    public void IsValidDepth_Returns_False_For_Invalid_Depths(string depth)
    {
        // Act & Assert
        Assert.False(GuildSettingsService.IsValidDepth(depth));
    }

    [Fact]
    public async Task GetOrCreateAsync_Creates_Settings_For_New_Guild()
    {
        // Act
        var settings = await _service.GetOrCreateAsync(99999UL);

        // Assert
        Assert.NotNull(settings);
        Assert.Equal(99999UL, settings.GuildId);
        Assert.Equal(GuildSettingsService.DefaultDepth, settings.PreferredSummaryDepth);
        Assert.False(settings.AutoSummarizeEnabled);
    }

    [Fact]
    public async Task GetAutoSummarizeEnabledAsync_Returns_False_By_Default()
    {
        // Act
        var enabled = await _service.GetAutoSummarizeEnabledAsync(12345UL);

        // Assert
        Assert.False(enabled);
    }

    [Fact]
    public async Task SetAutoSummarizeEnabledAsync_Persists_Value()
    {
        // Arrange
        var guildId = 12345UL;

        // Act
        await _service.SetAutoSummarizeEnabledAsync(guildId, true);
        var enabled = await _service.GetAutoSummarizeEnabledAsync(guildId);

        // Assert
        Assert.True(enabled);
    }
}
