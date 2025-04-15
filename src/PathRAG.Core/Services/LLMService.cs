using Azure.AI.OpenAI;
using Microsoft.Extensions.Options;

namespace PathRAG.Core.Services;

public class LLMService : ILLMService
{
    private readonly OpenAIClient _openAIClient;
    private readonly PathRagOptions _options;

    public LLMService(
        OpenAIClient openAIClient,
        IOptions<PathRagOptions> options)
    {
        _openAIClient = openAIClient;
        _options = options.Value;
    }

    public async Task<string> GenerateResponseAsync(
        string prompt,
        CancellationToken cancellationToken = default)
    {
        var messages = new List<ChatRequestMessage>
        {
            new ChatRequestSystemMessage("You are a helpful AI assistant that provides accurate and relevant information based on the given context."),
            new ChatRequestUserMessage(prompt)
        };

        var response = await _openAIClient.GetChatCompletionsAsync(
            new ChatCompletionsOptions(_options.CompletionModel, messages),
            cancellationToken
        );

        return response.Value.Choices[0].Message.Content;
    }
}