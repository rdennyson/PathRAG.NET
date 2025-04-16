namespace PathRAG.Core.Services.Cache;

public interface ILLMResponseCacheService
{
    Task<string?> GetResponseAsync(
        string query,
        CancellationToken cancellationToken = default
    );

    Task<string?> GetSimilarResponseAsync(
        string query,
        CancellationToken cancellationToken = default
    );

    Task CacheResponseAsync(
        string query,
        string response,
        CancellationToken cancellationToken = default
    );
}