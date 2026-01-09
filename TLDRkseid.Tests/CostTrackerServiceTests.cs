using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using TLDRkseid.Data;
using TLDRkseid.Services;

namespace TLDRkseid.Tests;

public class CostTrackerServiceTests : IDisposable
{
    private readonly DbContextOptions<TldrDbContext> _options;
    private readonly IDbContextFactory<TldrDbContext> _factory;
    private readonly CostTrackerService _service;

    public CostTrackerServiceTests()
    {
        _options = new DbContextOptionsBuilder<TldrDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var factoryMock = new Mock<IDbContextFactory<TldrDbContext>>();
        factoryMock.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new TldrDbContext(_options));
        factoryMock.Setup(f => f.CreateDbContext())
            .Returns(() => new TldrDbContext(_options));
        _factory = factoryMock.Object;

        var loggerMock = new Mock<ILogger<CostTrackerService>>();
        _service = new CostTrackerService(_factory, loggerMock.Object);
    }

    public void Dispose()
    {
        using var context = new TldrDbContext(_options);
        context.Database.EnsureDeleted();
    }

    #region AddAsync Tests

    [Fact]
    public async Task AddAsync_Creates_New_Entry_For_Guild()
    {
        var guildId = 12345UL;
        var cost = 0.005;

        await _service.AddAsync(guildId, cost);
        var total = await _service.GetGuildTotalAsync(guildId);

        Assert.Equal(cost, total, 6);
    }

    [Fact]
    public async Task AddAsync_Accumulates_Cost_For_Same_Guild()
    {
        var guildId = 12345UL;

        await _service.AddAsync(guildId, 0.001);
        await _service.AddAsync(guildId, 0.002);
        await _service.AddAsync(guildId, 0.003);

        var total = await _service.GetGuildTotalAsync(guildId);
        Assert.Equal(0.006, total, 6);
    }

    [Fact]
    public async Task AddAsync_Tracks_Tokens()
    {
        var guildId = 12345UL;

        await _service.AddAsync(guildId, 0.001, 100);
        await _service.AddAsync(guildId, 0.002, 200);

        var stats = await _service.GetGuildStatsAsync(guildId);
        Assert.Equal(300, stats.TotalTokens);
    }

    [Fact]
    public async Task AddAsync_Increments_Request_Count()
    {
        var guildId = 12345UL;

        await _service.AddAsync(guildId, 0.001);
        await _service.AddAsync(guildId, 0.002);
        await _service.AddAsync(guildId, 0.003);

        var count = await _service.GetGuildRequestCountTodayAsync(guildId);
        Assert.Equal(3, count);
    }

    [Fact]
    public async Task AddAsync_Ignores_Zero_Cost()
    {
        var guildId = 12345UL;

        await _service.AddAsync(guildId, 0);
        await _service.AddAsync(guildId, -1);

        var total = await _service.GetGuildTotalAsync(guildId);
        Assert.Equal(0, total);
    }

    [Fact]
    public async Task AddAsync_Legacy_Overload_Uses_GuildId_Zero()
    {
        await _service.AddAsync(0.005);

        var total = await _service.GetGuildTotalAsync(0);
        Assert.Equal(0.005, total, 6);
    }

    #endregion

    #region GetTotal Tests

    [Fact]
    public async Task GetTotalAsync_Returns_Zero_When_Empty()
    {
        var total = await _service.GetTotalAsync();
        Assert.Equal(0, total);
    }

    [Fact]
    public async Task GetTotalAsync_Returns_Sum_Across_All_Guilds()
    {
        await _service.AddAsync(11111UL, 0.001);
        await _service.AddAsync(22222UL, 0.002);
        await _service.AddAsync(33333UL, 0.003);

        var total = await _service.GetTotalAsync();
        Assert.Equal(0.006, total, 6);
    }

    [Fact]
    public async Task GetTotal_Sync_Returns_Correct_Value()
    {
        await _service.AddAsync(12345UL, 0.005);

        var total = _service.GetTotal();
        Assert.Equal(0.005, total, 6);
    }

    #endregion

    #region GetGuildTodayAsync Tests

    [Fact]
    public async Task GetGuildTodayAsync_Returns_Zero_For_New_Guild()
    {
        var result = await _service.GetGuildTodayAsync(12345UL);
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task GetGuildTodayAsync_Returns_Todays_Cost()
    {
        var guildId = 12345UL;
        await _service.AddAsync(guildId, 0.005);

        var result = await _service.GetGuildTodayAsync(guildId);
        Assert.Equal(0.005, result, 6);
    }

    #endregion

    #region GetGuildRequestCountTodayAsync Tests

    [Fact]
    public async Task GetGuildRequestCountTodayAsync_Returns_Zero_For_New_Guild()
    {
        var result = await _service.GetGuildRequestCountTodayAsync(12345UL);
        Assert.Equal(0, result);
    }

    #endregion

    #region GetGuildStatsAsync Tests

    [Fact]
    public async Task GetGuildStatsAsync_Returns_Zeros_For_New_Guild()
    {
        var stats = await _service.GetGuildStatsAsync(12345UL);

        Assert.Equal(12345UL, stats.GuildId);
        Assert.Equal(0, stats.TotalCost);
        Assert.Equal(0, stats.TotalRequests);
        Assert.Equal(0, stats.TotalTokens);
        Assert.Equal(0, stats.TodayCost);
        Assert.Equal(0, stats.TodayRequests);
        Assert.Equal(0, stats.TodayTokens);
    }

    [Fact]
    public async Task GetGuildStatsAsync_Returns_Correct_Stats()
    {
        var guildId = 12345UL;
        await _service.AddAsync(guildId, 0.001, 100);
        await _service.AddAsync(guildId, 0.002, 200);

        var stats = await _service.GetGuildStatsAsync(guildId);

        Assert.Equal(guildId, stats.GuildId);
        Assert.Equal(0.003, stats.TotalCost, 6);
        Assert.Equal(2, stats.TotalRequests);
        Assert.Equal(300, stats.TotalTokens);
        Assert.Equal(0.003, stats.TodayCost, 6);
        Assert.Equal(2, stats.TodayRequests);
        Assert.Equal(300, stats.TodayTokens);
    }

    #endregion

    #region GetGlobalStatsAsync Tests

    [Fact]
    public async Task GetGlobalStatsAsync_Returns_Zeros_When_Empty()
    {
        var stats = await _service.GetGlobalStatsAsync();

        Assert.Equal(0, stats.TotalCost);
        Assert.Equal(0, stats.TotalRequests);
        Assert.Equal(0, stats.TotalTokens);
        Assert.Equal(0, stats.UniqueGuilds);
        Assert.Equal(0, stats.TodayCost);
        Assert.Equal(0, stats.TodayRequests);
    }

    [Fact]
    public async Task GetGlobalStatsAsync_Returns_Aggregated_Stats()
    {
        await _service.AddAsync(11111UL, 0.001, 100);
        await _service.AddAsync(22222UL, 0.002, 200);
        await _service.AddAsync(33333UL, 0.003, 300);

        var stats = await _service.GetGlobalStatsAsync();

        Assert.Equal(0.006, stats.TotalCost, 6);
        Assert.Equal(3, stats.TotalRequests);
        Assert.Equal(600, stats.TotalTokens);
        Assert.Equal(3, stats.UniqueGuilds);
        Assert.Equal(0.006, stats.TodayCost, 6);
        Assert.Equal(3, stats.TodayRequests);
    }

    [Fact]
    public async Task GetGlobalStatsAsync_Counts_Unique_Guilds_Correctly()
    {
        await _service.AddAsync(11111UL, 0.001);
        await _service.AddAsync(11111UL, 0.001);
        await _service.AddAsync(22222UL, 0.001);

        var stats = await _service.GetGlobalStatsAsync();

        Assert.Equal(2, stats.UniqueGuilds);
    }

    #endregion

    #region Guild Isolation Tests

    [Fact]
    public async Task Different_Guilds_Have_Separate_Costs()
    {
        var guild1 = 11111UL;
        var guild2 = 22222UL;

        await _service.AddAsync(guild1, 0.005);
        await _service.AddAsync(guild2, 0.010);

        Assert.Equal(0.005, await _service.GetGuildTotalAsync(guild1), 6);
        Assert.Equal(0.010, await _service.GetGuildTotalAsync(guild2), 6);
    }

    #endregion
}
