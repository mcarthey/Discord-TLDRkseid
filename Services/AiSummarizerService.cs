using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Interfaces;
using OpenAI.Managers;
using OpenAI.ObjectModels;
using OpenAI.ObjectModels.RequestModels;

namespace DiscordPA.Services;

public class AiSummarizerService
{
    private readonly IOpenAIService _service;
    private readonly CostTrackerService _costTracker;
    private readonly ILogger<AiSummarizerService> _logger;
    private DateTime _lastRequestUtc = DateTime.MinValue;

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

        var result = await _service.ChatCompletion.CreateCompletion(request);

        var totalTokens = result.Usage.TotalTokens;
        var cost = totalTokens * 0.002 / 1000.0;

        await _costTracker.AddAsync(cost);

        if (result.Successful && result.Choices != null && result.Choices.Any())
        {
            var message = result.Choices.First().Message;
            if (message != null && !string.IsNullOrWhiteSpace(message.Content))
            {
                return (message.Content.Trim(), cost);
            }
        }

        if (!result.Successful)
        {
            _logger.LogError("OpenAI request failed. Status: {StatusCode}, Error: {ErrorMessage}",
                result.HttpStatusCode, result.Error?.Message ?? "(none)");

            if (result.HttpStatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                return ("⚠️ The Oracle is overwhelmed. Try again in a moment.", 0);
            }
        }

        return ("⚠️ AI summarization failed or returned no results.", 0);
    }
}
