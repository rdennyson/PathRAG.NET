namespace PathRAG.Core.Services;

public interface ILLMService
{
    Task<string> GenerateResponseAsync(
        string prompt,
        CancellationToken cancellationToken = default
    );
}