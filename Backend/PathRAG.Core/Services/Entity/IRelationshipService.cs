using PathRAG.Core.Models;
using PathRAG.Infrastructure.Models;

namespace PathRAG.Core.Services.Entity;

public interface IRelationshipService
{
    Task<List<Relationship>> ExtractRelationshipsAsync(
        List<GraphEntity> entities,
        string sourceText,
        CancellationToken cancellationToken = default);
        
    Task<List<Relationship>> FindRelatedEntitiesAsync(
        string entityId,
        int maxDepth = 2,
        CancellationToken cancellationToken = default);
        
    Task<double> CalculateRelationshipStrengthAsync(
        string sourceEntityId,
        string targetEntityId,
        CancellationToken cancellationToken = default);
        
    Task<List<Relationship>> GetRelationshipPathAsync(
        string sourceEntityId,
        string targetEntityId,
        CancellationToken cancellationToken = default);
}