using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using OpenAI.Interfaces;
using OpenAI.ObjectModels.RequestModels;
using OpenAI.ObjectModels.ResponseModels;
using OpenAI.ObjectModels.SharedModels;
using System.Net;
using System.Reflection;
using TLDRkseid.Data;
using TLDRkseid.Services;

namespace TLDRkseid.Tests;

public class AiSummarizerServiceTests : IDisposable
{
    private readonly Mock<IOpenAIService> _mockOpenAi;
    private readonly Mock<IChatCompletionService> _mockChatCompletion;
    private readonly CostTrackerService _costTracker;
    private readonly Mock<ILogger<AiSummarizerService>> _mockLogger;
    private readonly AiSummarizerService _service;
    private readonly DbContextOptions<TldrDbContext> _dbOptions;

    public AiSummarizerServiceTests()
    {
        _dbOptions = new DbContextOptionsBuilder<TldrDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var factoryMock = new Mock<IDbContextFactory<TldrDbContext>>();
        factoryMock.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new TldrDbContext(_dbOptions));
        factoryMock.Setup(f => f.CreateDbContext())
            .Returns(() => new TldrDbContext(_dbOptions));

        var costLoggerMock = new Mock<ILogger<CostTrackerService>>();
        _costTracker = new CostTrackerService(factoryMock.Object, costLoggerMock.Object);

        _mockChatCompletion = new Mock<IChatCompletionService>();
        _mockOpenAi = new Mock<IOpenAIService>();
        _mockOpenAi.Setup(x => x.ChatCompletion).Returns(_mockChatCompletion.Object);

        _mockLogger = new Mock<ILogger<AiSummarizerService>>();

        _service = new AiSummarizerService(_mockOpenAi.Object, _costTracker, _mockLogger.Object);
    }

    public void Dispose()
    {
        using var context = new TldrDbContext(_dbOptions);
        context.Database.EnsureDeleted();
    }

    // Helper to create response using reflection (since properties are read-only)
    private static ChatCompletionCreateResponse CreateSuccessResponse(string content, int tokens = 100)
    {
        var response = new ChatCompletionCreateResponse();

        // Use reflection to set read-only properties
        SetProperty(response, "Successful", true);

        response.Choices = new List<ChatChoiceResponse>
        {
            new ChatChoiceResponse
            {
                Message = ChatMessage.FromAssistant(content)
            }
        };

        response.Usage = new UsageResponse
        {
            TotalTokens = tokens
        };

        return response;
    }

    private static ChatCompletionCreateResponse CreateErrorResponse(HttpStatusCode statusCode)
    {
        var response = new ChatCompletionCreateResponse();
        SetProperty(response, "Successful", false);
        response.HttpStatusCode = statusCode;
        return response;
    }

    private static void SetProperty(object obj, string propertyName, object value)
    {
        var prop = obj.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (prop != null && prop.CanWrite)
        {
            prop.SetValue(obj, value);
        }
        else
        {
            // Try to find backing field
            var field = obj.GetType().GetField($"<{propertyName}>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
            field?.SetValue(obj, value);
        }
    }

    #region Successful Response Tests

    [Fact]
    public async Task SummarizeAsync_Returns_Summary_On_Success()
    {
        // Arrange
        var messages = new List<string> { "user1: Hello", "user2: Hi there" };
        var expectedSummary = "• Users greeted each other";

        _mockChatCompletion
            .Setup(x => x.CreateCompletion(It.IsAny<ChatCompletionCreateRequest>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSuccessResponse(expectedSummary, 150));

        // Act
        var (summary, cost) = await _service.SummarizeAsync(messages);

        // Assert
        Assert.Equal(expectedSummary, summary);
        Assert.True(cost > 0);
    }

    [Fact]
    public async Task SummarizeAsync_Trims_Whitespace_From_Response()
    {
        // Arrange
        var messages = new List<string> { "user1: Hello" };

        _mockChatCompletion
            .Setup(x => x.CreateCompletion(It.IsAny<ChatCompletionCreateRequest>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSuccessResponse("  Summary with whitespace  \n", 100));

        // Act
        var (summary, _) = await _service.SummarizeAsync(messages);

        // Assert
        Assert.Equal("Summary with whitespace", summary);
    }

    [Fact]
    public async Task SummarizeAsync_Tracks_Cost()
    {
        // Arrange
        var messages = new List<string> { "user1: Hello" };
        var tokens = 500;
        var expectedCost = tokens * 0.002 / 1000.0;

        _mockChatCompletion
            .Setup(x => x.CreateCompletion(It.IsAny<ChatCompletionCreateRequest>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSuccessResponse("Summary", tokens));

        // Act
        var (_, cost) = await _service.SummarizeAsync(messages);

        // Assert
        Assert.Equal(expectedCost, cost, 6);

        // Verify cost was tracked
        var totalCost = await _costTracker.GetTotalAsync();
        Assert.Equal(expectedCost, totalCost, 6);
    }

    [Fact]
    public async Task SummarizeAsync_Joins_Messages_With_Newlines()
    {
        // Arrange
        var messages = new List<string> { "msg1", "msg2", "msg3" };
        ChatCompletionCreateRequest? capturedRequest = null;

        _mockChatCompletion
            .Setup(x => x.CreateCompletion(It.IsAny<ChatCompletionCreateRequest>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<ChatCompletionCreateRequest, string?, CancellationToken>((req, _, _) => capturedRequest = req)
            .ReturnsAsync(CreateSuccessResponse("Summary", 100));

        // Act
        await _service.SummarizeAsync(messages);

        // Assert
        Assert.NotNull(capturedRequest);
        var userMessage = capturedRequest.Messages.Last().Content;
        Assert.Contains("msg1\nmsg2\nmsg3", userMessage);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task SummarizeAsync_Returns_Error_Message_On_Unauthorized()
    {
        // Arrange
        var messages = new List<string> { "user1: Hello" };

        _mockChatCompletion
            .Setup(x => x.CreateCompletion(It.IsAny<ChatCompletionCreateRequest>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateErrorResponse(HttpStatusCode.Unauthorized));

        // Act
        var (summary, cost) = await _service.SummarizeAsync(messages);

        // Assert
        Assert.Contains("API authentication failed", summary);
        Assert.Equal(0, cost);
    }

    [Fact]
    public async Task SummarizeAsync_Returns_Error_Message_On_RateLimit()
    {
        // Arrange
        var messages = new List<string> { "user1: Hello" };

        _mockChatCompletion
            .Setup(x => x.CreateCompletion(It.IsAny<ChatCompletionCreateRequest>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateErrorResponse(HttpStatusCode.TooManyRequests));

        // Act
        var (summary, cost) = await _service.SummarizeAsync(messages);

        // Assert
        Assert.Contains("rate limited", summary);
        Assert.Equal(0, cost);
    }

    [Fact]
    public async Task SummarizeAsync_Returns_Error_Message_On_ServerError()
    {
        // Arrange
        var messages = new List<string> { "user1: Hello" };

        _mockChatCompletion
            .Setup(x => x.CreateCompletion(It.IsAny<ChatCompletionCreateRequest>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateErrorResponse(HttpStatusCode.InternalServerError));

        // Act
        var (summary, cost) = await _service.SummarizeAsync(messages);

        // Assert
        Assert.Contains("OpenAI is experiencing issues", summary);
        Assert.Equal(0, cost);
    }

    [Fact]
    public async Task SummarizeAsync_Returns_Error_Message_On_BadRequest()
    {
        // Arrange
        var messages = new List<string> { "user1: Hello" };

        _mockChatCompletion
            .Setup(x => x.CreateCompletion(It.IsAny<ChatCompletionCreateRequest>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateErrorResponse(HttpStatusCode.BadRequest));

        // Act
        var (summary, cost) = await _service.SummarizeAsync(messages);

        // Assert
        Assert.Contains("Invalid request", summary);
        Assert.Equal(0, cost);
    }

    [Fact]
    public async Task SummarizeAsync_Returns_Generic_Error_On_Unknown_Status()
    {
        // Arrange
        var messages = new List<string> { "user1: Hello" };

        _mockChatCompletion
            .Setup(x => x.CreateCompletion(It.IsAny<ChatCompletionCreateRequest>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateErrorResponse(HttpStatusCode.NotFound));

        // Act
        var (summary, cost) = await _service.SummarizeAsync(messages);

        // Assert
        Assert.Contains("summarization failed", summary);
        Assert.Equal(0, cost);
    }

    [Fact]
    public async Task SummarizeAsync_Returns_Network_Error_On_HttpRequestException()
    {
        // Arrange
        var messages = new List<string> { "user1: Hello" };

        _mockChatCompletion
            .Setup(x => x.CreateCompletion(It.IsAny<ChatCompletionCreateRequest>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Connection failed"));

        // Act
        var (summary, cost) = await _service.SummarizeAsync(messages);

        // Assert
        Assert.Contains("Network error", summary);
        Assert.Equal(0, cost);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task SummarizeAsync_Handles_Empty_Choices()
    {
        // Arrange
        var messages = new List<string> { "user1: Hello" };
        var response = CreateSuccessResponse("", 50);
        response.Choices = new List<ChatChoiceResponse>();

        _mockChatCompletion
            .Setup(x => x.CreateCompletion(It.IsAny<ChatCompletionCreateRequest>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        var (summary, _) = await _service.SummarizeAsync(messages);

        // Assert
        Assert.Contains("failed", summary.ToLower());
    }

    [Fact]
    public async Task SummarizeAsync_Uses_Correct_Model()
    {
        // Arrange
        var messages = new List<string> { "user1: Hello" };
        ChatCompletionCreateRequest? capturedRequest = null;

        _mockChatCompletion
            .Setup(x => x.CreateCompletion(It.IsAny<ChatCompletionCreateRequest>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<ChatCompletionCreateRequest, string?, CancellationToken>((req, _, _) => capturedRequest = req)
            .ReturnsAsync(CreateSuccessResponse("Summary", 100));

        // Act
        await _service.SummarizeAsync(messages);

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.Equal("gpt-3.5-turbo", capturedRequest.Model);
    }

    [Fact]
    public async Task SummarizeAsync_Does_Not_Track_Zero_Cost()
    {
        // Arrange
        var messages = new List<string> { "user1: Hello" };

        _mockChatCompletion
            .Setup(x => x.CreateCompletion(It.IsAny<ChatCompletionCreateRequest>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSuccessResponse("Summary", 0));

        // Act
        var (_, cost) = await _service.SummarizeAsync(messages);

        // Assert
        Assert.Equal(0, cost);

        var totalCost = await _costTracker.GetTotalAsync();
        Assert.Equal(0, totalCost);
    }

    [Fact]
    public async Task SummarizeAsync_Verifies_Api_Called_Once()
    {
        // Arrange
        var messages = new List<string> { "user1: Hello" };

        _mockChatCompletion
            .Setup(x => x.CreateCompletion(It.IsAny<ChatCompletionCreateRequest>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSuccessResponse("Summary", 100));

        // Act
        await _service.SummarizeAsync(messages);

        // Assert
        _mockChatCompletion.Verify(
            x => x.CreateCompletion(It.IsAny<ChatCompletionCreateRequest>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion
}
