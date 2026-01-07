using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Interfaces;
using OpenAI.Managers;
using OpenAI.ObjectModels;
using OpenAI.ObjectModels.RequestModels;
using System.Net;

namespace TLDRkseid.Services;

public class AiSummarizerService
{
    private readonly IOpenAIService _service;
    private readonly CostTrackerService _costTracker;
    private readonly ILogger<AiSummarizerService> _logger;
    private DateTime _lastRequestUtc = DateTime.MinValue;

    // Timeout for OpenAI API calls (Discord deferred responses allow up to 15 min, but we want reasonable UX)
    private static readonly TimeSpan ApiTimeout = TimeSpan.FromSeconds(30);

    public AiSummarizerService(string apiKey, CostTrackerService costTracker, ILogger<AiSummarizerService> logger)
    {
        _service = new OpenAIService(new OpenAiOptions { ApiKey = apiKey });
        _costTracker = costTracker;
        _logger = logger;
    }

    public async Task<(string Summary, double Cost)> SummarizeAsync(List<string> messages)
    {
        // Debounce protection
        var now = DateTime.UtcNow;
        var timeSinceLast = now - _lastRequestUtc;
        if (timeSinceLast.TotalSeconds < 2)
        {
            _logger.LogDebug("Debouncing AI call ({ElapsedMs}ms since last call)", timeSinceLast.TotalMilliseconds);
            await Task.Delay(2000 - (int)timeSinceLast.TotalMilliseconds);
        }
        _lastRequestUtc = DateTime.UtcNow;

        var chatContent = string.Join("\n", messages);
        var prompt = $@"
            You're a Discord assistant summarizing a channel conversation.

            Your job is to extract **any meaningful insights**, **recurring themes**, or **noteworthy quotes**, 
            even if the conversation is light, social, or mostly jokes. Always provide a summary, even if the discussion is minimal.

            Format the summary as 3–5 short bullet points.

            Never return an empty summary. Always produce something.

            Messages:
            {chatContent}
        ";

        var request = new ChatCompletionCreateRequest
        {
            Model = "gpt-3.5-turbo",
            Temperature = 0.7f,
            MaxTokens = 300,
            Messages = new List<ChatMessage>
            {
                ChatMessage.FromSystem("You are a helpful, concise summarizer that always provides a summary, even for casual conversation or light activity."),
                ChatMessage.FromUser(prompt)
            }
        };

        // Execute with timeout
        try
        {
            var apiTask = _service.ChatCompletion.CreateCompletion(request);
            var completedTask = await Task.WhenAny(apiTask, Task.Delay(ApiTimeout));

            if (completedTask != apiTask)
            {
                throw new OperationCanceledException($"OpenAI API call timed out after {ApiTimeout.TotalSeconds}s");
            }

            var result = await apiTask;

            var totalTokens = result.Usage?.TotalTokens ?? 0;
            var cost = totalTokens * 0.002 / 1000.0;

            if (cost > 0)
            {
                await _costTracker.AddAsync(cost);
            }

            if (result.Successful && result.Choices != null && result.Choices.Any())
            {
                var message = result.Choices.First().Message;
                if (message != null && !string.IsNullOrWhiteSpace(message.Content))
                {
                    return (message.Content.Trim(), cost);
                }
            }

            // Categorize and handle specific error types
            return HandleOpenAiError(result.HttpStatusCode, result.Error?.Message);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("OpenAI API call timed out after {Timeout}s", ApiTimeout.TotalSeconds);
            return ("⏱️ The summary request timed out. The channel may have too many messages to process. Try a smaller depth.", 0);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error during OpenAI API call");
            return ("🌐 Network error while contacting the AI service. Please try again.", 0);
        }
    }

    private (string Summary, double Cost) HandleOpenAiError(HttpStatusCode? statusCode, string? errorMessage)
    {
        _logger.LogError("OpenAI request failed. Status: {StatusCode}, Error: {ErrorMessage}",
            statusCode, errorMessage ?? "(none)");

        return statusCode switch
        {
            HttpStatusCode.Unauthorized =>
                ("🔑 API authentication failed. Please contact the bot administrator.", 0),

            HttpStatusCode.TooManyRequests =>
                ("⚠️ The Oracle is overwhelmed (rate limited). Try again in a moment.", 0),

            HttpStatusCode.InternalServerError or HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable =>
                ("🔧 OpenAI is experiencing issues. Please try again later.", 0),

            HttpStatusCode.BadRequest =>
                ("❌ Invalid request. The message content may be too long or contain unsupported characters.", 0),

            _ => ("⚠️ AI summarization failed. Please try again later.", 0)
        };
    }
}
