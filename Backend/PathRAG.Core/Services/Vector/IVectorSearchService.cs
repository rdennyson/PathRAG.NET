using PathRAG.Core.Models;
using PathRAG.Infrastructure.Models;

namespace PathRAG.Core.Services.Vector;

public interface IVectorSearchService
{
    Task<IReadOnlyList<TextChunk>> SearchTextChunksAsync(
        float[] queryEmbedding,
        int topK,
        CancellationToken cancellationToken = default,
        List<Guid>? vectorStoreIds = null);

    Task<IList<GraphEntity>> SearchEntitiesAsync(
        float[] queryEmbedding,
        int topK,
        CancellationToken cancellationToken = default,
        List<Guid>? vectorStoreIds = null);

    Task<IReadOnlyList<Relationship>> SearchRelationshipsAsync(
        float[] queryEmbedding,
        int topK,
        CancellationToken cancellationToken = default,
        List<Guid>? vectorStoreIds = null);
}
