namespace PathRAG.Core.Services.Cache;

public interface IEmbeddingCacheService
{
    Task<float[]?> GetEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default);

    Task<(bool found, float[]? embedding)> TryGetEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default);

    Task<(float[]? embedding, string? originalText)> GetSimilarEmbeddingAsync(
        string text,
        float[] queryEmbedding,
        float similarityThreshold = 0.95f,
        CancellationToken cancellationToken = default);

    Task CacheEmbeddingAsync(
        string text,
        float[] embedding,
        CancellationToken cancellationToken = default);

    Task<bool> HasEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default);

    Task RemoveEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default);
}