using Microsoft.Extensions.Configuration;
using Npgsql;
using PathRAG.Core.Models;

namespace PathRAG.Core.Services.Graph;

public class PostgresAGEGraphStorageService : IGraphStorageService, IDisposable
{
    private readonly NpgsqlConnection _connection;
    private readonly string _graphName;
    private readonly Func<NpgsqlConnection> _connectionFactory;
    private bool _disposed = false;

    public PostgresAGEGraphStorageService(IConfiguration config)
        : this(config, () => new NpgsqlConnection(config.GetConnectionString("DefaultConnection")))
    {
    }

    // Constructor with connection factory for testing
    public PostgresAGEGraphStorageService(IConfiguration config, Func<NpgsqlConnection> connectionFactory)
    {
        _connectionFactory = connectionFactory;
        _connection = _connectionFactory();
        _graphName = config["PathRAG:GraphName"] ?? "pathrag";
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await ExecuteCypherQuery<bool>($"CREATE GRAPH IF NOT EXISTS {_graphName}", cancellationToken);
    }

    public async Task<bool> HasNodeAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        var query = $"SELECT * FROM ag_catalog.ag_graph WHERE name = '{_graphName}' AND EXISTS (SELECT 1 FROM ag_catalog.ag_vertex WHERE id = '{nodeId}')";
        return await ExecuteCypherQuery<bool>(query, cancellationToken);
    }

    public async Task<bool> HasEdgeAsync(string sourceNodeId, string targetNodeId, CancellationToken cancellationToken = default)
    {
        var query = $"SELECT EXISTS (SELECT 1 FROM ag_catalog.ag_edge WHERE start_id = '{sourceNodeId}' AND end_id = '{targetNodeId}')";
        return await ExecuteCypherQuery<bool>(query, cancellationToken);
    }

    public async Task<int> GetNodeDegreeAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        var query = $"SELECT count(*) FROM ag_catalog.ag_edge WHERE start_id = '{nodeId}' OR end_id = '{nodeId}'";
        return await ExecuteCypherQuery<int>(query, cancellationToken);
    }

    public async Task<int> GetEdgeDegreeAsync(string sourceNodeId, string targetNodeId, CancellationToken cancellationToken = default)
    {
        var query = $"SELECT count(*) FROM ag_catalog.ag_edge WHERE (start_id = '{sourceNodeId}' OR end_id = '{sourceNodeId}') OR (start_id = '{targetNodeId}' OR end_id = '{targetNodeId}')";
        return await ExecuteCypherQuery<int>(query, cancellationToken);
    }

    public async Task<float> GetPageRankAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        // Simplified PageRank implementation using node degree as a proxy
        // In a real implementation, you would use a proper PageRank algorithm
        var nodeDegree = await GetNodeDegreeAsync(nodeId, cancellationToken);
        var totalNodes = await GetTotalNodeCountAsync(cancellationToken);

        if (totalNodes == 0) return 0;

        // Simple normalization
        return (float)nodeDegree / totalNodes;
    }

    private async Task<int> GetTotalNodeCountAsync(CancellationToken cancellationToken)
    {
        var query = $"SELECT count(*) FROM ag_catalog.ag_vertex WHERE graph_name = '{_graphName}'";
        return await ExecuteCypherQuery<int>(query, cancellationToken);
    }

    public async Task<Dictionary<string, object>> GetNodeAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        var query = $"SELECT properties FROM ag_catalog.ag_vertex WHERE id = '{nodeId}'";
        return await ExecuteCypherQuery<Dictionary<string, object>>(query, cancellationToken);
    }

    public async Task<Dictionary<string, object>> GetEdgeAsync(string sourceNodeId, string targetNodeId, CancellationToken cancellationToken = default)
    {
        var query = $"SELECT properties FROM ag_catalog.ag_edge WHERE start_id = '{sourceNodeId}' AND end_id = '{targetNodeId}'";
        return await ExecuteCypherQuery<Dictionary<string, object>>(query, cancellationToken);
    }

    public async Task AddEntitiesAndRelationshipsAsync(List<GraphEntity> entities, List<Relationship> relationships, CancellationToken cancellationToken = default)
    {
        foreach (var entity in entities)
        {
            var query = $"CREATE (n:Entity {{id: '{entity.Id}', name: '{entity.Name}', type: '{entity.Type}', description: '{entity.Description}'}})";
            await ExecuteCypherQuery<bool>(query, cancellationToken);
        }

        foreach (var relationship in relationships)
        {
            var query = $"MATCH (a:Entity {{id: '{relationship.SourceEntityId}'}}), (b: {{id: '{relationship.TargetEntityId}'}}) CREATE (a)-[r:{relationship.Type} {{description: '{relationship.Description}'}}]->(b)";
            await ExecuteCypherQuery<bool>(query, cancellationToken);
        }
    }

    public async Task<float[]> EmbedNodesAsync(string algorithm = "node2vec", CancellationToken cancellationToken = default)
    {
        // This is a simplified implementation
        // In a real implementation, you would use a proper graph embedding algorithm

        // Get all nodes
        var query = $"SELECT id, properties FROM ag_catalog.ag_vertex WHERE graph_name = '{_graphName}'";

        // Ensure connection is open
        if (_connection.State != System.Data.ConnectionState.Open)
        {
            await _connection.OpenAsync(cancellationToken);
        }

        try
        {
            var nodeIds = new List<string>();
            var nodeProperties = new List<Dictionary<string, object>>();

            using var cmd = new NpgsqlCommand(query, _connection);
            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                var nodeId = reader.GetString(0);
                nodeIds.Add(nodeId);

                var props = new Dictionary<string, object>();
                if (!reader.IsDBNull(1))
                {
                    var jsonProps = reader.GetString(1);
                    props = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(jsonProps)
                        ?? new Dictionary<string, object>();
                }
                nodeProperties.Add(props);
            }

            // Create a simple embedding based on node properties
            // This is just a placeholder - real implementations would use proper graph embedding algorithms
            var embeddingSize = 1536; // Match the embedding size used in the rest of the system
            var embeddings = new float[nodeIds.Count * embeddingSize];

            // Fill with random values as a placeholder
            var random = new Random(42); // Fixed seed for reproducibility
            for (int i = 0; i < embeddings.Length; i++)
            {
                embeddings[i] = (float)random.NextDouble() * 2 - 1; // Values between -1 and 1
            }

            return embeddings;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error embedding nodes: {ex.Message}");
            throw;
        }
    }

    private async Task<T> ExecuteCypherQuery<T>(string query, CancellationToken cancellationToken)
    {
        // Ensure connection is open
        if (_connection.State != System.Data.ConnectionState.Open)
        {
            await _connection.OpenAsync(cancellationToken);
        }

        try
        {
            using var cmd = new NpgsqlCommand(query, _connection);

            if (typeof(T) == typeof(bool))
            {
                // For existence checks
                using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                var result = await reader.ReadAsync(cancellationToken);
                return (T)(object)(result && !reader.IsDBNull(0) && reader.GetBoolean(0));
            }
            else if (typeof(T) == typeof(int))
            {
                // For count queries
                var result = await cmd.ExecuteScalarAsync(cancellationToken);
                return (T)(object)Convert.ToInt32(result);
            }
            else if (typeof(T) == typeof(float))
            {
                // For numeric results like PageRank
                var result = await cmd.ExecuteScalarAsync(cancellationToken);
                return (T)(object)Convert.ToSingle(result);
            }
            else if (typeof(T) == typeof(Dictionary<string, object>))
            {
                // For node/edge property queries
                using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                if (await reader.ReadAsync(cancellationToken))
                {
                    var result = new Dictionary<string, object>();

                    // Handle AGE JSON properties
                    if (!reader.IsDBNull(0))
                    {
                        var jsonProperties = reader.GetString(0);
                        var properties = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(jsonProperties);
                        if (properties != null)
                        {
                            foreach (var prop in properties)
                            {
                                result[prop.Key] = prop.Value;
                            }
                        }
                    }

                    return (T)(object)result;
                }
                return (T)(object)new Dictionary<string, object>();
            }

            // Default fallback
            throw new NotImplementedException($"Unhandled return type: {typeof(T).Name}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error executing query: {ex.Message}");
            throw;
        }
    }

    public async Task<List<Relationship>> FindShortestPathAsync(
    string sourceEntityId,
    string targetEntityId,
    CancellationToken cancellationToken = default)
    {
        var query = @"
        SELECT * FROM ag_catalog.ag_shortest_path(
            $$ MATCH p = shortestPath((source:Entity {id: $1})-[*]->(target:Entity {id: $2}))
               RETURN relationships(p) AS rels $$,
            ARRAY[$1, $2]
        ) AS path";

        var relationships = new List<Relationship>();

        // Ensure connection is open
        if (_connection.State != System.Data.ConnectionState.Open)
        {
            await _connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var cmd = new NpgsqlCommand(query, _connection);
            cmd.Parameters.AddWithValue("$1", sourceEntityId);
            cmd.Parameters.AddWithValue("$2", targetEntityId);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var relationship = new Relationship
                {
                    SourceEntityId = reader.GetString(reader.GetOrdinal("source_id")),
                    TargetEntityId = reader.GetString(reader.GetOrdinal("target_id")),
                    Type = reader.GetString(reader.GetOrdinal("type")),
                    Description = reader.GetString(reader.GetOrdinal("description"))
                };
                relationships.Add(relationship);
            }

            return relationships;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error finding shortest path: {ex.Message}");
            throw;
        }
    }

    public async Task<IEnumerable<object>> GetRelatedNodesAsync(
        string keyword,
        CancellationToken cancellationToken = default)
    {
        var query = @"
        SELECT * FROM ag_catalog.ag_catalog_search(
            $$ MATCH (n:Entity)
               WHERE n.name =~ $1 OR n.description =~ $1
               OPTIONAL MATCH (n)-[r]-(related)
               RETURN n, collect(r) as rels, collect(related) as related_nodes $$,
            ARRAY[$1]
        ) AS results";

        var results = new List<object>();

        // Ensure connection is open
        if (_connection.State != System.Data.ConnectionState.Open)
        {
            await _connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var cmd = new NpgsqlCommand(query, _connection);
            cmd.Parameters.AddWithValue("$1", $"(?i).*{keyword}.*");

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                // Add entities
                if (!reader.IsDBNull(reader.GetOrdinal("n")))
                {
                    var entity = new GraphEntity
                    {
                        Id = reader.GetGuid(reader.GetOrdinal("id")),
                        Name = reader.GetString(reader.GetOrdinal("name")),
                        Type = reader.GetString(reader.GetOrdinal("type")),
                        Description = reader.GetString(reader.GetOrdinal("description"))
                    };
                    results.Add(entity);
                }

                // Add relationships
                if (!reader.IsDBNull(reader.GetOrdinal("rels")))
                {
                    var relationship = new Relationship
                    {
                        SourceEntityId = reader.GetString(reader.GetOrdinal("source_id")),
                        TargetEntityId = reader.GetString(reader.GetOrdinal("target_id")),
                        Type = reader.GetString(reader.GetOrdinal("type")),
                        Description = reader.GetString(reader.GetOrdinal("description"))
                    };
                    results.Add(relationship);
                }
            }

            return results;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting related nodes: {ex.Message}");
            throw;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _connection?.Close();
                _connection?.Dispose();
            }

            _disposed = true;
        }
    }

    ~PostgresAGEGraphStorageService()
    {
        Dispose(false);
    }
}