namespace PathRAG.Core.Services.Embedding;

public interface IEmbeddingService
{
    Task<float[]> GetEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<float[]>> GetEmbeddingsAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default
    );
}