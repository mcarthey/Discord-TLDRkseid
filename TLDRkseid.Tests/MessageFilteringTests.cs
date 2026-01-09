using Discord;
using Moq;

namespace TLDRkseid.Tests;

public class MessageFilteringTests
{
    /// <summary>
    /// Simulates the message filtering logic from TldrModule.cs lines 162-167.
    /// This tests that null Authors are safely handled.
    /// </summary>
    private static List<string> FilterMessages(IEnumerable<IMessage> messages, IUser? userFilter = null)
    {
        return messages
            .Where(m => m.Author != null && !m.Author.IsBot)
            .Where(m => userFilter == null || m.Author.Id == userFilter.Id)
            .OrderBy(m => m.Timestamp)
            .Select(m => $"{m.Author.Username}: {m.Content}")
            .ToList();
    }

    private static Mock<IMessage> CreateMockMessage(string username, string content, bool isBot = false, IUser? author = null)
    {
        var mockMessage = new Mock<IMessage>();

        if (author != null)
        {
            mockMessage.Setup(m => m.Author).Returns(author);
        }
        else if (username != null)
        {
            var mockAuthor = new Mock<IUser>();
            mockAuthor.Setup(a => a.Username).Returns(username);
            mockAuthor.Setup(a => a.IsBot).Returns(isBot);
            mockAuthor.Setup(a => a.Id).Returns((ulong)username.GetHashCode());
            mockMessage.Setup(m => m.Author).Returns(mockAuthor.Object);
        }
        else
        {
            // Null author case
            mockMessage.Setup(m => m.Author).Returns((IUser)null!);
        }

        mockMessage.Setup(m => m.Content).Returns(content);
        mockMessage.Setup(m => m.Timestamp).Returns(DateTimeOffset.UtcNow);

        return mockMessage;
    }

    [Fact]
    public void FilterMessages_WithValidMessages_ReturnsFilteredList()
    {
        // Arrange
        var messages = new[]
        {
            CreateMockMessage("User1", "Hello").Object,
            CreateMockMessage("User2", "Hi there").Object
        };

        // Act
        var result = FilterMessages(messages);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains("User1: Hello", result);
        Assert.Contains("User2: Hi there", result);
    }

    [Fact]
    public void FilterMessages_WithNullAuthor_SkipsMessageWithoutException()
    {
        // Arrange - This is the exact scenario that was causing the /tldr hang
        var validMessage = CreateMockMessage("User1", "Hello");
        var nullAuthorMessage = new Mock<IMessage>();
        nullAuthorMessage.Setup(m => m.Author).Returns((IUser)null!);
        nullAuthorMessage.Setup(m => m.Content).Returns("System message");
        nullAuthorMessage.Setup(m => m.Timestamp).Returns(DateTimeOffset.UtcNow);

        var messages = new[] { validMessage.Object, nullAuthorMessage.Object };

        // Act - Should NOT throw NullReferenceException
        var result = FilterMessages(messages);

        // Assert
        Assert.Single(result);
        Assert.Contains("User1: Hello", result);
    }

    [Fact]
    public void FilterMessages_WithAllNullAuthors_ReturnsEmptyList()
    {
        // Arrange
        var nullAuthorMessage1 = new Mock<IMessage>();
        nullAuthorMessage1.Setup(m => m.Author).Returns((IUser)null!);
        nullAuthorMessage1.Setup(m => m.Content).Returns("System message 1");
        nullAuthorMessage1.Setup(m => m.Timestamp).Returns(DateTimeOffset.UtcNow);

        var nullAuthorMessage2 = new Mock<IMessage>();
        nullAuthorMessage2.Setup(m => m.Author).Returns((IUser)null!);
        nullAuthorMessage2.Setup(m => m.Content).Returns("System message 2");
        nullAuthorMessage2.Setup(m => m.Timestamp).Returns(DateTimeOffset.UtcNow);

        var messages = new[] { nullAuthorMessage1.Object, nullAuthorMessage2.Object };

        // Act
        var result = FilterMessages(messages);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void FilterMessages_ExcludesBotMessages()
    {
        // Arrange
        var humanMessage = CreateMockMessage("Human", "Hello").Object;
        var botMessage = CreateMockMessage("Bot", "I am a bot", isBot: true).Object;

        var messages = new[] { humanMessage, botMessage };

        // Act
        var result = FilterMessages(messages);

        // Assert
        Assert.Single(result);
        Assert.Contains("Human: Hello", result);
    }

    [Fact]
    public void FilterMessages_WithUserFilter_OnlyIncludesMatchingUser()
    {
        // Arrange
        var targetUser = new Mock<IUser>();
        targetUser.Setup(u => u.Id).Returns(12345UL);
        targetUser.Setup(u => u.Username).Returns("TargetUser");
        targetUser.Setup(u => u.IsBot).Returns(false);

        var targetMessage = CreateMockMessage(null!, "Target's message", author: targetUser.Object).Object;
        var otherMessage = CreateMockMessage("OtherUser", "Other's message").Object;

        var messages = new[] { targetMessage, otherMessage };

        // Act
        var result = FilterMessages(messages, targetUser.Object);

        // Assert
        Assert.Single(result);
        Assert.Contains("TargetUser: Target's message", result);
    }

    [Fact]
    public void FilterMessages_MixedScenario_HandlesAllCasesCorrectly()
    {
        // Arrange - Mix of null authors, bots, and valid messages
        var validMessage1 = CreateMockMessage("User1", "First message").Object;

        var nullAuthorMessage = new Mock<IMessage>();
        nullAuthorMessage.Setup(m => m.Author).Returns((IUser)null!);
        nullAuthorMessage.Setup(m => m.Content).Returns("System notification");
        nullAuthorMessage.Setup(m => m.Timestamp).Returns(DateTimeOffset.UtcNow);

        var botMessage = CreateMockMessage("TLDRkseid", "Bot response", isBot: true).Object;
        var validMessage2 = CreateMockMessage("User2", "Second message").Object;

        var messages = new[]
        {
            validMessage1,
            nullAuthorMessage.Object,
            botMessage,
            validMessage2
        };

        // Act
        var result = FilterMessages(messages);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains("User1: First message", result);
        Assert.Contains("User2: Second message", result);
    }

    [Fact]
    public void FilterMessages_PreservesMessageOrder()
    {
        // Arrange
        var earlier = new Mock<IMessage>();
        var laterUser = new Mock<IUser>();
        laterUser.Setup(u => u.Username).Returns("Later");
        laterUser.Setup(u => u.IsBot).Returns(false);
        earlier.Setup(m => m.Author).Returns(laterUser.Object);
        earlier.Setup(m => m.Content).Returns("First");
        earlier.Setup(m => m.Timestamp).Returns(DateTimeOffset.UtcNow.AddMinutes(-5));

        var later = new Mock<IMessage>();
        var earlierUser = new Mock<IUser>();
        earlierUser.Setup(u => u.Username).Returns("Earlier");
        earlierUser.Setup(u => u.IsBot).Returns(false);
        later.Setup(m => m.Author).Returns(earlierUser.Object);
        later.Setup(m => m.Content).Returns("Second");
        later.Setup(m => m.Timestamp).Returns(DateTimeOffset.UtcNow);

        // Pass in wrong order to verify sorting
        var messages = new[] { later.Object, earlier.Object };

        // Act
        var result = FilterMessages(messages);

        // Assert - Should be ordered by timestamp
        Assert.Equal(2, result.Count);
        Assert.Equal("Later: First", result[0]); // Earlier timestamp first
        Assert.Equal("Earlier: Second", result[1]); // Later timestamp second
    }
}
