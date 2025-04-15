using PathRAG.Core.Models;

namespace PathRAG.Core.Services.Vector;

public interface IVectorSearchService
{
    Task<IReadOnlyList<TextChunk>> SearchTextChunksAsync(
        float[] queryEmbedding,
        int topK,
        CancellationToken cancellationToken = default);
        
    Task<IList<GraphEntity>> SearchEntitiesAsync(
        float[] queryEmbedding,
        int topK,
        CancellationToken cancellationToken = default);
        
    Task<IReadOnlyList<Relationship>> SearchRelationshipsAsync(
        float[] queryEmbedding,
        int topK,
        CancellationToken cancellationToken = default);
}
