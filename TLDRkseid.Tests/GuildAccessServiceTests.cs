using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using TLDRkseid.Data;
using TLDRkseid.Services;

namespace TLDRkseid.Tests;

public class GuildAccessServiceTests : IDisposable
{
    private readonly DbContextOptions<TldrDbContext> _options;
    private readonly IDbContextFactory<TldrDbContext> _factory;
    private readonly SpamBlockerService _spamBlocker;
    private readonly GuildAccessService _service;

    public GuildAccessServiceTests()
    {
        _options = new DbContextOptionsBuilder<TldrDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var factoryMock = new Mock<IDbContextFactory<TldrDbContext>>();
        factoryMock.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new TldrDbContext(_options));
        _factory = factoryMock.Object;

        _spamBlocker = new SpamBlockerService();
        var loggerMock = new Mock<ILogger<GuildAccessService>>();
        _service = new GuildAccessService(_factory, loggerMock.Object, _spamBlocker);
    }

    public void Dispose()
    {
        _spamBlocker.Dispose();
    }

    #region Superuser Tests

    [Fact]
    public async Task IsSuperuserAsync_Returns_False_For_New_Guild()
    {
        var result = await _service.IsSuperuserAsync(12345UL, 99999UL);
        Assert.False(result);
    }

    [Fact]
    public async Task TryAssignSuperuserAsync_Succeeds_For_New_Guild()
    {
        var guildId = 12345UL;
        var userId = 99999UL;

        var result = await _service.TryAssignSuperuserAsync(guildId, userId);

        Assert.True(result);
        Assert.True(await _service.IsSuperuserAsync(guildId, userId));
    }

    [Fact]
    public async Task TryAssignSuperuserAsync_Fails_If_Superuser_Already_Exists()
    {
        var guildId = 12345UL;
        var firstUser = 11111UL;
        var secondUser = 22222UL;

        await _service.TryAssignSuperuserAsync(guildId, firstUser);
        var result = await _service.TryAssignSuperuserAsync(guildId, secondUser);

        Assert.False(result);
        Assert.True(await _service.IsSuperuserAsync(guildId, firstUser));
        Assert.False(await _service.IsSuperuserAsync(guildId, secondUser));
    }

    [Fact]
    public async Task GetSuperuserAsync_Returns_Null_For_New_Guild()
    {
        var result = await _service.GetSuperuserAsync(12345UL);
        Assert.Null(result);
    }

    [Fact]
    public async Task GetSuperuserAsync_Returns_Superuser_Id()
    {
        var guildId = 12345UL;
        var userId = 99999UL;
        await _service.TryAssignSuperuserAsync(guildId, userId);

        var result = await _service.GetSuperuserAsync(guildId);

        Assert.Equal(userId, result);
    }

    [Fact]
    public async Task TransferSuperuserAsync_Succeeds()
    {
        var guildId = 12345UL;
        var oldSuperuser = 11111UL;
        var newSuperuser = 22222UL;
        await _service.TryAssignSuperuserAsync(guildId, oldSuperuser);

        var result = await _service.TransferSuperuserAsync(guildId, newSuperuser, oldSuperuser);

        Assert.True(result);
        Assert.False(await _service.IsSuperuserAsync(guildId, oldSuperuser));
        Assert.True(await _service.IsSuperuserAsync(guildId, newSuperuser));
    }

    [Fact]
    public async Task TransferSuperuserAsync_Fails_If_No_Existing_Superuser()
    {
        var result = await _service.TransferSuperuserAsync(12345UL, 22222UL, 11111UL);
        Assert.False(result);
    }

    [Fact]
    public async Task RevokeSuperuserAsync_Succeeds()
    {
        var guildId = 12345UL;
        var userId = 99999UL;
        await _service.TryAssignSuperuserAsync(guildId, userId);

        var result = await _service.RevokeSuperuserAsync(guildId, userId);

        Assert.True(result);
        Assert.False(await _service.IsSuperuserAsync(guildId, userId));
        Assert.Null(await _service.GetSuperuserAsync(guildId));
    }

    [Fact]
    public async Task RevokeSuperuserAsync_Fails_If_No_Existing_Superuser()
    {
        var result = await _service.RevokeSuperuserAsync(12345UL, 99999UL);
        Assert.False(result);
    }

    #endregion

    #region Admin Tests

    [Fact]
    public async Task IsAdminAsync_Returns_False_For_New_Guild()
    {
        var result = await _service.IsAdminAsync(12345UL, 99999UL);
        Assert.False(result);
    }

    [Fact]
    public async Task AddAdminAsync_Succeeds()
    {
        var guildId = 12345UL;
        var userId = 99999UL;

        var result = await _service.AddAdminAsync(guildId, userId);

        Assert.True(result);
        Assert.True(await _service.IsAdminAsync(guildId, userId));
    }

    [Fact]
    public async Task AddAdminAsync_Returns_False_If_Already_Admin()
    {
        var guildId = 12345UL;
        var userId = 99999UL;
        await _service.AddAdminAsync(guildId, userId);

        var result = await _service.AddAdminAsync(guildId, userId);

        Assert.False(result);
    }

    [Fact]
    public async Task RemoveAdminAsync_Succeeds()
    {
        var guildId = 12345UL;
        var userId = 99999UL;
        await _service.AddAdminAsync(guildId, userId);

        var result = await _service.RemoveAdminAsync(guildId, userId);

        Assert.True(result);
        Assert.False(await _service.IsAdminAsync(guildId, userId));
    }

    [Fact]
    public async Task RemoveAdminAsync_Returns_False_If_Not_Admin()
    {
        var result = await _service.RemoveAdminAsync(12345UL, 99999UL);
        Assert.False(result);
    }

    [Fact]
    public async Task GetAdminsAsync_Returns_Empty_For_New_Guild()
    {
        var result = await _service.GetAdminsAsync(12345UL);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAdminsAsync_Returns_All_Admins()
    {
        var guildId = 12345UL;
        await _service.AddAdminAsync(guildId, 11111UL);
        await _service.AddAdminAsync(guildId, 22222UL);
        await _service.AddAdminAsync(guildId, 33333UL);

        var result = await _service.GetAdminsAsync(guildId);

        Assert.Equal(3, result.Count);
        Assert.Contains(11111UL, result);
        Assert.Contains(22222UL, result);
        Assert.Contains(33333UL, result);
    }

    #endregion

    #region CanAccessAdminFeatures Tests

    [Fact]
    public async Task CanAccessAdminFeaturesAsync_Returns_False_For_Non_Admin_Non_Superuser()
    {
        var result = await _service.CanAccessAdminFeaturesAsync(12345UL, 99999UL);
        Assert.False(result);
    }

    [Fact]
    public async Task CanAccessAdminFeaturesAsync_Returns_True_For_Superuser()
    {
        var guildId = 12345UL;
        var userId = 99999UL;
        await _service.TryAssignSuperuserAsync(guildId, userId);

        var result = await _service.CanAccessAdminFeaturesAsync(guildId, userId);

        Assert.True(result);
    }

    [Fact]
    public async Task CanAccessAdminFeaturesAsync_Returns_True_For_Admin()
    {
        var guildId = 12345UL;
        var userId = 99999UL;
        await _service.AddAdminAsync(guildId, userId);

        var result = await _service.CanAccessAdminFeaturesAsync(guildId, userId);

        Assert.True(result);
    }

    #endregion

    #region Guild Isolation Tests

    [Fact]
    public async Task Different_Guilds_Have_Separate_Superusers()
    {
        var guild1 = 11111UL;
        var guild2 = 22222UL;
        var user1 = 99999UL;
        var user2 = 88888UL;

        await _service.TryAssignSuperuserAsync(guild1, user1);
        await _service.TryAssignSuperuserAsync(guild2, user2);

        Assert.True(await _service.IsSuperuserAsync(guild1, user1));
        Assert.False(await _service.IsSuperuserAsync(guild1, user2));
        Assert.False(await _service.IsSuperuserAsync(guild2, user1));
        Assert.True(await _service.IsSuperuserAsync(guild2, user2));
    }

    [Fact]
    public async Task Different_Guilds_Have_Separate_Admins()
    {
        var guild1 = 11111UL;
        var guild2 = 22222UL;
        var user = 99999UL;

        await _service.AddAdminAsync(guild1, user);

        Assert.True(await _service.IsAdminAsync(guild1, user));
        Assert.False(await _service.IsAdminAsync(guild2, user));
    }

    #endregion

    #region Rate Limiting Tests

    [Fact]
    public void IsRateLimited_Returns_False_Initially()
    {
        var result = _service.IsRateLimited(12345UL, out var reason);
        Assert.False(result);
        Assert.Empty(reason);
    }

    #endregion
}
