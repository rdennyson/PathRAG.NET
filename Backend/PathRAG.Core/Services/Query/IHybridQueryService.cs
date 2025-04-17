using PathRAG.Core.Models;
using PathRAG.Infrastructure.Models;

namespace PathRAG.Core.Services.Query;

public record SearchResult(
    IReadOnlyList<TextChunk> Chunks,
    IReadOnlyList<GraphEntity> Entities,
    IReadOnlyList<Relationship> Relationships
);

public interface IHybridQueryService
{
    Task<SearchResult> SearchAsync(
        float[] queryEmbedding,
        IReadOnlyList<string> highLevelKeywords,
        IReadOnlyList<string> lowLevelKeywords,
        CancellationToken cancellationToken = default
    );

    Task<List<TextChunk>> SemanticSearchAsync(
        float[] queryEmbedding,
        List<Guid> vectorStoreIds,
        int topK,
        CancellationToken cancellationToken = default);

    Task<List<TextChunk>> HybridSearchAsync(
        string query,
        float[] queryEmbedding,
        List<Guid> vectorStoreIds,
        int topK,
        CancellationToken cancellationToken = default,
        IReadOnlyList<string>? highLevelKeywords = null,
        IReadOnlyList<string>? lowLevelKeywords = null);

    Task<(List<TextChunk> chunks, List<GraphEntity> entities, List<Relationship> relationships)> GraphSearchAsync(
        string query,
        float[] queryEmbedding,
        List<Guid> vectorStoreIds,
        int topK,
        CancellationToken cancellationToken = default,
        IReadOnlyList<string>? highLevelKeywords = null,
        IReadOnlyList<string>? lowLevelKeywords = null);
}