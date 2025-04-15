using Azure.AI.OpenAI;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace PathRAG.Core.Services.Query;

public class KeywordExtractionService : IKeywordExtractionService
{
    private readonly OpenAIClient _openAIClient;
    private readonly PathRagOptions _options;

    public KeywordExtractionService(
        OpenAIClient openAIClient,
        IOptions<PathRagOptions> options)
    {
        _openAIClient = openAIClient;
        _options = options.Value;
    }

    public async Task<KeywordExtractionResult> ExtractKeywordsAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        var messages = new List<ChatRequestMessage>
        {
            new ChatRequestSystemMessage("Extract high-level and low-level keywords from the given text."),
            new ChatRequestUserMessage(text)
        };

        var response = await _openAIClient.GetChatCompletionsAsync(
            new ChatCompletionsOptions(_options.KeywordExtractionModel, messages),
            cancellationToken
        );

        var jsonResponse = response.Value.Choices[0].Message.Content;
        var keywords = JsonSerializer.Deserialize<KeywordExtractionResponse>(jsonResponse);

        return new KeywordExtractionResult(
            keywords.HighLevelKeywords,
            keywords.LowLevelKeywords
        );
    }

    private class KeywordExtractionResponse
    {
        public List<string> HighLevelKeywords { get; set; } = new();
        public List<string> LowLevelKeywords { get; set; } = new();
    }
}