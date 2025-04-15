using PathRAG.Core.Models;

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
}