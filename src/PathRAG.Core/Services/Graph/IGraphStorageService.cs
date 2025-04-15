using PathRAG.Core.Models;

namespace PathRAG.Core.Services.Graph;

public interface IGraphStorageService
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<bool> HasNodeAsync(string nodeId, CancellationToken cancellationToken = default);
    Task<bool> HasEdgeAsync(string sourceNodeId, string targetNodeId, CancellationToken cancellationToken = default);
    Task<int> GetNodeDegreeAsync(string nodeId, CancellationToken cancellationToken = default);
    Task<int> GetEdgeDegreeAsync(string sourceNodeId, string targetNodeId, CancellationToken cancellationToken = default);
    Task<float> GetPageRankAsync(string nodeId, CancellationToken cancellationToken = default);
    Task<Dictionary<string, object>> GetNodeAsync(string nodeId, CancellationToken cancellationToken = default);
    Task<Dictionary<string, object>> GetEdgeAsync(string sourceNodeId, string targetNodeId, CancellationToken cancellationToken = default);
    Task AddEntitiesAndRelationshipsAsync(List<GraphEntity> entities, List<Relationship> relationships, CancellationToken cancellationToken = default);
    Task<float[]> EmbedNodesAsync(string algorithm = "node2vec", CancellationToken cancellationToken = default);
    Task<IEnumerable<object>> GetRelatedNodesAsync(string keyword, CancellationToken cancellationToken = default);
    Task<List<Relationship>> FindShortestPathAsync(string sourceEntityId, string targetEntityId, CancellationToken cancellationToken = default);
}