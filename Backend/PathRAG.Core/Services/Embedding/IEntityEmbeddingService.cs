using PathRAG.Core.Models;

namespace PathRAG.Core.Services.Embedding;

public interface IEntityEmbeddingService
{
    Task<float[]> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default);
    Task<List<float[]>> GetEmbeddingsAsync(List<string> texts, CancellationToken cancellationToken = default);
    Task<int> GetTokenCount(string text);
    Task<Dictionary<string, float[]>> GetNodeEmbeddingsAsync(
        IEnumerable<GraphEntity> entities,
        string algorithm = "node2vec",
        CancellationToken cancellationToken = default);
}