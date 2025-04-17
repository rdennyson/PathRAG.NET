using Azure.AI.OpenAI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace PathRAG.Core.Services.Query;

public class KeywordExtractionService : IKeywordExtractionService
{
    private readonly OpenAIClient _openAIClient;
    private readonly PathRagOptions _options;
    private readonly ILogger<KeywordExtractionService> _logger;

    public KeywordExtractionService(
        OpenAIClient openAIClient,
        IOptions<PathRagOptions> options,
        ILogger<KeywordExtractionService> logger)
    {
        _openAIClient = openAIClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<KeywordExtractionResult> ExtractKeywordsAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Create a more specific prompt that requests JSON format
            var systemPrompt = @"Extract high-level and low-level keywords from the given text. 

                High-level keywords are broader concepts or main topics. 
                Low-level keywords are specific terms, entities, or details.

                Return your response as a JSON object with the following structure:
                {
                  ""high_level_keywords"": [""keyword1"", ""keyword2"", ...],
                  ""low_level_keywords"": [""keyword1"", ""keyword2"", ...]
                }

                Ensure your response is a valid JSON object and nothing else.";

            var messages = new List<ChatRequestMessage>
            {
                new ChatRequestSystemMessage(systemPrompt),
                new ChatRequestUserMessage(text)
            };

            // Configure the model to return JSON
            var options = new ChatCompletionsOptions(_options.KeywordExtractionModel, messages)
            {
                ResponseFormat = ChatCompletionsResponseFormat.JsonObject
            };

            var response = await _openAIClient.GetChatCompletionsAsync(options, cancellationToken);
            var jsonResponse = response.Value.Choices[0].Message.Content;

            _logger.LogInformation("Received keyword extraction response: {Response}", jsonResponse);

            // Try to parse the JSON response
            var keywords = ParseJsonResponse(jsonResponse);

            return new KeywordExtractionResult(
                keywords.HighLevelKeywords,
                keywords.LowLevelKeywords
            );
        }
        catch (Exception ex)
        {
            // Fallback to empty lists if parsing fails
            _logger.LogError(ex, "Error extracting keywords: {Message}", ex.Message);
            return new KeywordExtractionResult(
                Array.Empty<string>(),
                Array.Empty<string>()
            );
        }
    }

    private KeywordExtractionResponse ParseJsonResponse(string jsonResponse)
    {
        try
        {
            // Try to deserialize directly
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var result = JsonSerializer.Deserialize<KeywordExtractionResponse>(jsonResponse, options);

            // If deserialization succeeded but the properties are null or empty, try alternative property names
            if ((result?.HighLevelKeywords == null || result.HighLevelKeywords.Count == 0) &&
                (result?.LowLevelKeywords == null || result.LowLevelKeywords.Count == 0))
            {
                // Try alternative property names (snake_case)
                var alternativeResult = JsonSerializer.Deserialize<AlternativeKeywordResponse>(jsonResponse, options);
                if (alternativeResult != null)
                {
                    return new KeywordExtractionResponse
                    {
                        HighLevelKeywords = alternativeResult.HighLevelKeywords ?? new List<string>(),
                        LowLevelKeywords = alternativeResult.LowLevelKeywords ?? new List<string>()
                    };
                }
            }

            return result ?? new KeywordExtractionResponse();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing JSON response: {Message}", ex.Message);
            return new KeywordExtractionResponse();
        }
    }

    private class KeywordExtractionResponse
    {
        public List<string> HighLevelKeywords { get; set; } = new();
        public List<string> LowLevelKeywords { get; set; } = new();
    }

    private class AlternativeKeywordResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("high_level_keywords")]
        public List<string>? HighLevelKeywords { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("low_level_keywords")]
        public List<string>? LowLevelKeywords { get; set; }
    }
}
