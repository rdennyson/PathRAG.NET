using PathRAG.Core.Models;
using PathRAG.Infrastructure.Models;

namespace PathRAG.Core.Services.Query;

public interface IContextBuilderService
{
    Task<string> BuildContextAsync(
        IReadOnlyList<TextChunk> chunks,
        IReadOnlyList<GraphEntity> entities,
        IReadOnlyList<Relationship> relationships,
        CancellationToken cancellationToken = default
    );
}