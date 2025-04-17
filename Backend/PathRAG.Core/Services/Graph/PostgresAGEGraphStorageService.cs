using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using PathRAG.Core.Models;
using PathRAG.Infrastructure.Models;
using System.Text.Json;

namespace PathRAG.Core.Services.Graph;

public class PostgresAGEGraphStorageService : IGraphStorageService, IDisposable
{
    private readonly NpgsqlConnection _connection;
    private readonly string _graphName;
    private readonly Func<NpgsqlConnection> _connectionFactory;
    private readonly ILogger<PostgresAGEGraphStorageService> _logger;
    private bool _disposed = false;

    public PostgresAGEGraphStorageService(IConfiguration config, ILogger<PostgresAGEGraphStorageService> logger)
        : this(config, () => new NpgsqlConnection(config.GetConnectionString("DefaultConnection")), logger)
    {
    }

    // Constructor with connection factory for testing
    public PostgresAGEGraphStorageService(IConfiguration config, Func<NpgsqlConnection> connectionFactory, ILogger<PostgresAGEGraphStorageService> logger)
    {
        _connectionFactory = connectionFactory;
        _connection = _connectionFactory();
        _graphName = config["PathRAG:GraphName"] ?? "pathrag";
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Initializing graph: {GraphName}", _graphName);

        // Ensure connection is open
        if (_connection.State != System.Data.ConnectionState.Open)
        {
            await _connection.OpenAsync(cancellationToken);
        }

        try
        {
            // First, check if the AGE extension is installed
            _logger.LogInformation("Checking AGE extension...");
            bool ageExtensionInstalled = await IsExtensionInstalledAsync("age", cancellationToken);

            if (!ageExtensionInstalled)
            {
                _logger.LogWarning("AGE extension is not installed. Installing it now...");
                await InstallAgeExtensionAsync(cancellationToken);
            }

            // Load the AGE extension
            _logger.LogInformation("Loading AGE extension...");
            try
            {
                using var loadCmd = new NpgsqlCommand("LOAD 'age';", _connection);
                await loadCmd.ExecuteNonQueryAsync(cancellationToken);

                // Set the search path to include ag_catalog
                using var pathCmd = new NpgsqlCommand("SET search_path = ag_catalog, '$user', public;", _connection);
                await pathCmd.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Warning: Could not load AGE extension. Some graph functionality may be limited.");
                // Continue execution as we'll use fallback methods if needed
            }

            // Tables are now created by EF Core during application startup

            // Check if the graph exists
            try
            {
                var checkQuery = "SELECT * FROM ag_catalog.ag_graph WHERE name = @graphName";
                using (var cmd = new NpgsqlCommand(checkQuery, _connection))
                {
                    cmd.Parameters.AddWithValue("@graphName", _graphName);
                    using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                    if (!await reader.ReadAsync(cancellationToken))
                    {
                        // Graph doesn't exist, create it
                        reader.Close();

                        _logger.LogInformation("Creating new graph: {GraphName}", _graphName);
                        var createQuery = "SELECT * FROM ag_catalog.create_graph(@graphName)";
                        using var createCmd = new NpgsqlCommand(createQuery, _connection);
                        createCmd.Parameters.AddWithValue("@graphName", _graphName);
                        await createCmd.ExecuteNonQueryAsync(cancellationToken);
                    }
                    else
                    {
                        _logger.LogInformation("Graph {GraphName} already exists", _graphName);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Warning: Could not check or create graph. Will use fallback storage methods.");
                // Continue execution as we'll use fallback methods if needed
            }
        }

        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing graph: {GraphName}", _graphName);
            // Don't throw here, as we want the application to continue even if graph functionality is limited
        }
    }

    private async Task<bool> IsExtensionInstalledAsync(string extensionName, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = "SELECT 1 FROM pg_extension WHERE extname = @extensionName";
            using var cmd = new NpgsqlCommand(query, _connection);
            cmd.Parameters.AddWithValue("@extensionName", extensionName);
            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            return await reader.ReadAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if extension {ExtensionName} is installed", extensionName);
            return false;
        }
    }

    private async Task InstallAgeExtensionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var query = "CREATE EXTENSION IF NOT EXISTS age;";
            using var cmd = new NpgsqlCommand(query, _connection);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
            _logger.LogInformation("AGE extension installed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error installing AGE extension");
            throw;
        }
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
        // Get all nodes and edges to build the graph
        var nodesQuery = $"SELECT id, properties FROM ag_catalog.ag_vertex WHERE graph_name = '{_graphName}'";
        var edgesQuery = $"SELECT start_id, end_id, properties FROM ag_catalog.ag_edge WHERE graph_name = '{_graphName}'";

        // Ensure connection is open
        if (_connection.State != System.Data.ConnectionState.Open)
        {
            await _connection.OpenAsync(cancellationToken);
        }

        try
        {
            // Build a graph using QuikGraph
            var graph = new QuikGraph.AdjacencyGraph<string, QuikGraph.Edge<string>>(true);

            // Get all nodes
            using (var cmd = new NpgsqlCommand(nodesQuery, _connection))
            using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    var id = reader.GetString(0);
                    graph.AddVertex(id);
                }
            }

            // Get all edges
            using (var cmd = new NpgsqlCommand(edgesQuery, _connection))
            using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    var sourceId = reader.GetString(0);
                    var targetId = reader.GetString(1);
                    var edge = new QuikGraph.Edge<string>(sourceId, targetId);
                    graph.AddEdge(edge);
                }
            }

            // Calculate PageRank
            var pageRankValues = await CalculatePageRankAsync(graph, cancellationToken);

            // Find the PageRank value for the specified node
            if (pageRankValues.TryGetValue(nodeId, out float pageRank))
            {
                return pageRank;
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error calculating PageRank: {ex.Message}");
            throw;
        }
    }

    private async Task<Dictionary<string, float>> CalculatePageRankAsync(
        QuikGraph.AdjacencyGraph<string, QuikGraph.Edge<string>> graph,
        CancellationToken cancellationToken)
    {
        // PageRank parameters
        double dampingFactor = 0.85;
        int maxIterations = 100;
        double convergenceThreshold = 1e-6;

        // Create a mapping from node to index
        var nodeToIndex = new Dictionary<string, int>();
        var indexToNode = new Dictionary<int, string>();
        int index = 0;

        foreach (var node in graph.Vertices)
        {
            nodeToIndex[node] = index;
            indexToNode[index] = node;
            index++;
        }

        int nodeCount = graph.VertexCount;
        var pageRank = new double[nodeCount];
        var newPageRank = new double[nodeCount];

        // Initialize PageRank values
        double initialValue = 1.0 / nodeCount;
        for (int i = 0; i < nodeCount; i++)
        {
            pageRank[i] = initialValue;
        }

        // Calculate PageRank
        bool converged = false;
        int iteration = 0;

        while (!converged && iteration < maxIterations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Reset new PageRank values
            for (int i = 0; i < nodeCount; i++)
            {
                newPageRank[i] = (1 - dampingFactor) / nodeCount;
            }

            // Update PageRank values
            foreach (var node in graph.Vertices)
            {
                int nodeIdx = nodeToIndex[node];
                var outEdges = graph.OutEdges(node).ToList();

                if (outEdges.Count > 0)
                {
                    double contribution = pageRank[nodeIdx] * dampingFactor / outEdges.Count;

                    foreach (var edge in outEdges)
                    {
                        int targetIdx = nodeToIndex[edge.Target];
                        newPageRank[targetIdx] += contribution;
                    }
                }
                else
                {
                    // Distribute to all nodes if no outgoing edges
                    double contribution = pageRank[nodeIdx] * dampingFactor / nodeCount;

                    for (int i = 0; i < nodeCount; i++)
                    {
                        newPageRank[i] += contribution;
                    }
                }
            }

            // Check for convergence
            double diff = 0;
            for (int i = 0; i < nodeCount; i++)
            {
                diff += Math.Abs(newPageRank[i] - pageRank[i]);
            }

            converged = diff < convergenceThreshold;

            // Update PageRank values
            for (int i = 0; i < nodeCount; i++)
            {
                pageRank[i] = newPageRank[i];
            }

            iteration++;
        }

        // Convert to dictionary mapping node ID to PageRank value
        var result = new Dictionary<string, float>();
        for (int i = 0; i < nodeCount; i++)
        {
            result[indexToNode[i]] = (float)pageRank[i];
        }

        return result;
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
        _logger.LogInformation("Adding {EntityCount} entities and {RelationshipCount} relationships", entities.Count, relationships.Count);

        try
        {
            // First try using the AGE Cypher approach
            await AddEntitiesAndRelationshipsWithAGEAsync(entities, relationships, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error using AGE Cypher for adding entities and relationships. Falling back to direct SQL approach.");

            // Fallback to a more direct SQL approach
            await AddEntitiesAndRelationshipsWithSQLAsync(entities, relationships, cancellationToken);
        }
    }

    // Helper method to format keywords for Cypher query
    private string FormatKeywordsForCypher(List<string> keywords)
    {
        if (keywords == null || keywords.Count == 0)
            return "[]"; // Empty array

        var formattedKeywords = keywords
            .Select(k => $"'{k.Replace("'", "''")}'") // Escape single quotes
            .ToList();

        return $"[{string.Join(",", formattedKeywords)}]"; // Format as array
    }

    private async Task AddEntitiesAndRelationshipsWithAGEAsync(List<GraphEntity> entities, List<Relationship> relationships, CancellationToken cancellationToken = default)
    {
        // Create entities
        foreach (var entity in entities)
        {
            // Escape single quotes in string properties
            var name = entity.Name.Replace("'", "''");
            var type = entity.Type.Replace("'", "''");
            var description = entity.Description.Replace("'", "''");
            var sourceId = entity.SourceId?.Replace("'", "''") ?? "";

            // Format properties directly in the Cypher query
            var query = $@"SELECT * FROM ag_catalog.cypher('{_graphName}', $$
                CREATE (n:Entity {{
                    id:            '{entity.Id}',
                    name:          '{name}',
                    type:          '{type}',
                    description:   '{description}',
                    keywords:      {FormatKeywordsForCypher(entity.Keywords)},
                    weight:        {entity.Weight},
                    sourceId:      '{sourceId}',
                    vectorStoreId: '{entity.VectorStoreId}',
                    createdAt:     '{entity.CreatedAt:o}'
                }})
                RETURN n
            $$) as (n agtype);";

            await ExecuteCypherQuery<bool>(query, cancellationToken);
        }

        // Create relationships
        foreach (var relationship in relationships)
        {
            // Escape single quotes in string properties
            var type = relationship.Type.Replace("'", "''");
            var description = relationship.Description.Replace("'", "''");
            var sourceId = relationship.SourceId?.Replace("'", "''") ?? "";

            // Format properties directly in the Cypher query
            var query = $@"SELECT * FROM ag_catalog.cypher('{_graphName}', $$
                MATCH (a:Entity), (b:Entity)
                WHERE a.id = '{relationship.SourceEntityId}' AND b.id = '{relationship.TargetEntityId}'
                CREATE (a)-[r:{type} {{
                    id:            '{relationship.Id}',
                    description:   '{description}',
                    weight:        {relationship.Weight},
                    keywords:      {FormatKeywordsForCypher(relationship.Keywords)},
                    sourceId:      '{sourceId}',
                    vectorStoreId: '{relationship.VectorStoreId}',
                    createdAt:     '{relationship.CreatedAt:o}'
                }}]->(b)
                RETURN r
            $$) as (r agtype);";

            await ExecuteCypherQuery<bool>(query, cancellationToken);
        }
    }

    private async Task AddEntitiesAndRelationshipsWithSQLAsync(List<GraphEntity> entities, List<Relationship> relationships, CancellationToken cancellationToken = default)
    {
        // Ensure connection is open
        if (_connection.State != System.Data.ConnectionState.Open)
        {
            await _connection.OpenAsync(cancellationToken);
        }

        // Create entities using direct SQL
        foreach (var entity in entities)
        {
            try
            {
                // Escape single quotes in string properties
                var name = entity.Name?.Replace("'", "''") ?? "";
                var type = entity.Type?.Replace("'", "''") ?? "";
                var description = entity.Description?.Replace("'", "''") ?? "";
                var sourceId = entity.SourceId?.Replace("'", "''") ?? "";

                // Create properties object with all entity properties
                var propertiesJson = JsonSerializer.Serialize(new
                {
                    id = entity.Id.ToString(),
                    name,
                    type,
                    description,
                    keywords = entity.Keywords ?? new List<string>(),
                    weight = entity.Weight,
                    sourceId,
                    vectorStoreId = entity.VectorStoreId.ToString(),
                    createdAt = entity.CreatedAt
                });

                // Use direct SQL to insert into the vertex table
                var query = $@"INSERT INTO ag_catalog.ag_vertex (graph_name, id, label, properties)
                    VALUES ('{_graphName}', '{entity.Id}', 'Entity', '{propertiesJson}'::jsonb)
                    ON CONFLICT (graph_name, id) DO UPDATE
                    SET properties = '{propertiesJson}'::jsonb";

                using var cmd = new NpgsqlCommand(query, _connection);
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inserting entity {EntityId}", entity.Id);
                // Continue with the next entity
            }
        }

        // Create relationships using direct SQL
        foreach (var relationship in relationships)
        {
            try
            {
                // Escape single quotes in string properties
                var type = relationship.Type?.Replace("'", "''") ?? "";
                var description = relationship.Description?.Replace("'", "''") ?? "";
                var sourceId = relationship.SourceId?.Replace("'", "''") ?? "";

                // Create properties object with all relationship properties
                var propertiesJson = JsonSerializer.Serialize(new
                {
                    id = relationship.Id.ToString(),
                    description,
                    weight = relationship.Weight,
                    keywords = relationship.Keywords ?? new List<string>(),
                    sourceId,
                    vectorStoreId = relationship.VectorStoreId.ToString(),
                    createdAt = relationship.CreatedAt
                });

                // Use direct SQL to insert into the edge table
                var query = $@"INSERT INTO ag_catalog.ag_edge (graph_name, start_id, end_id, label, properties)
                    VALUES ('{_graphName}', '{relationship.SourceEntityId}', '{relationship.TargetEntityId}', '{type}', '{propertiesJson}'::jsonb)
                    ON CONFLICT (graph_name, start_id, end_id, label) DO UPDATE
                    SET properties = '{propertiesJson}'::jsonb";

                using var cmd = new NpgsqlCommand(query, _connection);
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inserting relationship from {SourceId} to {TargetId}", relationship.SourceEntityId, relationship.TargetEntityId);
                // Continue with the next relationship
            }
        }
    }

    public async Task RemoveEntitiesAndRelationshipsAsync(List<string> entityIds, List<(string sourceId, string targetId)> relationshipIds, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Removing {RelationshipCount} relationships and {EntityCount} entities", relationshipIds.Count, entityIds.Count);

        try
        {
            // First try using the AGE Cypher approach
            await RemoveEntitiesAndRelationshipsWithAGEAsync(entityIds, relationshipIds, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error using AGE Cypher for removing entities and relationships. Falling back to direct SQL approach.");

            // Fallback to a more direct SQL approach
            await RemoveEntitiesAndRelationshipsWithSQLAsync(entityIds, relationshipIds, cancellationToken);
        }
    }

    private async Task RemoveEntitiesAndRelationshipsWithAGEAsync(List<string> entityIds, List<(string sourceId, string targetId)> relationshipIds, CancellationToken cancellationToken = default)
    {
        // Remove relationships first to maintain referential integrity
        foreach (var (sourceId, targetId) in relationshipIds)
        {
            // Use proper AGE syntax for deleting relationships
            var query = $@"SELECT * FROM ag_catalog.cypher('{_graphName}', $$
                MATCH (a:Entity)-[r]->(b:Entity)
                WHERE a.id = '{sourceId}' AND b.id = '{targetId}'
                DELETE r
                RETURN count(*)
            $$) as (count agtype);";

            await ExecuteCypherQuery<bool>(query, cancellationToken);
        }

        // Then remove entities
        foreach (var entityId in entityIds)
        {
            // Use proper AGE syntax for deleting entities
            var query = $@"SELECT * FROM ag_catalog.cypher('{_graphName}', $$
                MATCH (n:Entity)
                WHERE n.id = '{entityId}'
                DELETE n
                RETURN count(*)
            $$) as (count agtype);";

            await ExecuteCypherQuery<bool>(query, cancellationToken);
        }
    }

    private async Task RemoveEntitiesAndRelationshipsWithSQLAsync(List<string> entityIds, List<(string sourceId, string targetId)> relationshipIds, CancellationToken cancellationToken = default)
    {
        // Ensure connection is open
        if (_connection.State != System.Data.ConnectionState.Open)
        {
            await _connection.OpenAsync(cancellationToken);
        }

        // Remove relationships first to maintain referential integrity
        foreach (var (sourceId, targetId) in relationshipIds)
        {
            try
            {
                // Use direct SQL to delete from the edge table
                var query = $@"DELETE FROM ag_catalog.ag_edge
                    WHERE graph_name = '{_graphName}' AND start_id = '{sourceId}' AND end_id = '{targetId}'";

                using var cmd = new NpgsqlCommand(query, _connection);
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing relationship from {SourceId} to {TargetId}", sourceId, targetId);
                // Continue with the next relationship
            }
        }

        // Then remove entities
        foreach (var entityId in entityIds)
        {
            try
            {
                // Use direct SQL to delete from the vertex table
                var query = $@"DELETE FROM ag_catalog.ag_vertex
                    WHERE graph_name = '{_graphName}' AND id = '{entityId}'";

                using var cmd = new NpgsqlCommand(query, _connection);
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing entity {EntityId}", entityId);
                // Continue with the next entity
            }
        }
    }

    public async Task<float[]> EmbedNodesAsync(string algorithm = "node2vec", CancellationToken cancellationToken = default)
    {
        // Get all nodes and edges to build the graph
        var nodesQuery = $"SELECT id, properties FROM ag_catalog.ag_vertex WHERE graph_name = '{_graphName}'";
        var edgesQuery = $"SELECT start_id, end_id, properties FROM ag_catalog.ag_edge WHERE graph_name = '{_graphName}'";

        // Ensure connection is open
        if (_connection.State != System.Data.ConnectionState.Open)
        {
            await _connection.OpenAsync(cancellationToken);
        }

        try
        {
            // Build a graph using QuikGraph
            var graph = new QuikGraph.AdjacencyGraph<string, QuikGraph.Edge<string>>(true);
            var nodeProperties = new Dictionary<string, Dictionary<string, object>>();
            var edgeProperties = new Dictionary<(string, string), Dictionary<string, object>>();

            // Get all nodes
            using (var cmd = new NpgsqlCommand(nodesQuery, _connection))
            using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    var nodeId = reader.GetString(0);
                    var props = new Dictionary<string, object>();

                    if (!reader.IsDBNull(1))
                    {
                        var jsonProps = reader.GetString(1);
                        props = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(jsonProps)
                            ?? new Dictionary<string, object>();
                    }

                    graph.AddVertex(nodeId);
                    nodeProperties[nodeId] = props;
                }
            }

            // Get all edges
            using (var cmd = new NpgsqlCommand(edgesQuery, _connection))
            using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    var sourceId = reader.GetString(0);
                    var targetId = reader.GetString(1);
                    var props = new Dictionary<string, object>();

                    if (!reader.IsDBNull(2))
                    {
                        var jsonProps = reader.GetString(2);
                        props = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(jsonProps)
                            ?? new Dictionary<string, object>();
                    }

                    var edge = new QuikGraph.Edge<string>(sourceId, targetId);
                    graph.AddEdge(edge);
                    edgeProperties[(sourceId, targetId)] = props;
                }
            }

            // Choose the embedding algorithm based on the parameter
            float[] embeddings;
            switch (algorithm.ToLowerInvariant())
            {
                case "node2vec":
                    embeddings = await Node2VecEmbedAsync(graph, nodeProperties, edgeProperties, cancellationToken);
                    break;
                case "pagerank":
                    embeddings = await PageRankEmbedAsync(graph, cancellationToken);
                    break;
                default:
                    throw new ArgumentException($"Unsupported embedding algorithm: {algorithm}");
            }

            return embeddings;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error embedding nodes: {ex.Message}");
            throw;
        }
    }

    private async Task<float[]> Node2VecEmbedAsync(
        QuikGraph.AdjacencyGraph<string, QuikGraph.Edge<string>> graph,
        Dictionary<string, Dictionary<string, object>> nodeProperties,
        Dictionary<(string, string), Dictionary<string, object>> edgeProperties,
        CancellationToken cancellationToken)
    {
        // Node2Vec parameters
        int dimensions = 128;       // Embedding dimensions
        int walkLength = 80;        // Length of each random walk
        int numWalks = 10;          // Number of random walks per node
        double returnParameter = 1; // Return parameter (p)
        double inOutParameter = 1;  // In-out parameter (q)

        // Generate random walks
        var random = new Random(42); // Fixed seed for reproducibility
        var walks = new List<List<string>>();

        foreach (var node in graph.Vertices)
        {
            for (int i = 0; i < numWalks; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var walk = new List<string> { node };
                var currentNode = node;

                for (int j = 0; j < walkLength - 1; j++)
                {
                    var neighbors = graph.OutEdges(currentNode).Select(e => e.Target).ToList();
                    if (!neighbors.Any())
                        break;

                    // Simple random walk (without p/q parameters for simplicity)
                    int nextIdx = random.Next(neighbors.Count);
                    var nextNode = neighbors[nextIdx];
                    walk.Add(nextNode);
                    currentNode = nextNode;
                }

                walks.Add(walk);
            }
        }

        // Create a vocabulary of all nodes
        var nodeToIndex = new Dictionary<string, int>();
        int index = 0;
        foreach (var node in graph.Vertices)
        {
            nodeToIndex[node] = index++;
        }

        // Initialize embeddings with random values
        var embeddingSize = dimensions;
        var nodeCount = graph.VertexCount;
        var embeddings = new float[nodeCount * embeddingSize];

        // Fill with small random values
        for (int i = 0; i < embeddings.Length; i++)
        {
            embeddings[i] = (float)(random.NextDouble() * 0.2 - 0.1); // Small values between -0.1 and 0.1
        }

        // Simplified Skip-gram model training
        // In a real implementation, you would use proper Skip-gram with negative sampling
        double learningRate = 0.025;
        int windowSize = 5;
        int iterations = 5;

        for (int iter = 0; iter < iterations; iter++)
        {
            foreach (var walk in walks)
            {
                cancellationToken.ThrowIfCancellationRequested();

                for (int i = 0; i < walk.Count; i++)
                {
                    var currentNode = walk[i];
                    var currentNodeIdx = nodeToIndex[currentNode];

                    // Context window
                    int start = Math.Max(0, i - windowSize);
                    int end = Math.Min(walk.Count - 1, i + windowSize);

                    for (int j = start; j <= end; j++)
                    {
                        if (j == i) continue; // Skip the current node

                        var contextNode = walk[j];
                        var contextNodeIdx = nodeToIndex[contextNode];

                        // Update embeddings (simplified)
                        for (int k = 0; k < embeddingSize; k++)
                        {
                            float currentEmb = embeddings[currentNodeIdx * embeddingSize + k];
                            float contextEmb = embeddings[contextNodeIdx * embeddingSize + k];

                            // Very simplified update rule
                            float gradient = (float)(learningRate * (1 - currentEmb * contextEmb));
                            embeddings[currentNodeIdx * embeddingSize + k] += gradient * contextEmb;
                            embeddings[contextNodeIdx * embeddingSize + k] += gradient * currentEmb;
                        }
                    }
                }
            }

            // Decrease learning rate
            learningRate *= 0.9;
        }

        // Normalize embeddings
        for (int i = 0; i < nodeCount; i++)
        {
            float norm = 0;
            for (int j = 0; j < embeddingSize; j++)
            {
                norm += embeddings[i * embeddingSize + j] * embeddings[i * embeddingSize + j];
            }
            norm = (float)Math.Sqrt(norm);

            if (norm > 0)
            {
                for (int j = 0; j < embeddingSize; j++)
                {
                    embeddings[i * embeddingSize + j] /= norm;
                }
            }
        }

        return embeddings;
    }

    private async Task<float[]> PageRankEmbedAsync(
        QuikGraph.AdjacencyGraph<string, QuikGraph.Edge<string>> graph,
        CancellationToken cancellationToken)
    {
        // PageRank parameters
        double dampingFactor = 0.85;
        int maxIterations = 100;
        double convergenceThreshold = 1e-6;

        // Create a mapping from node to index
        var nodeToIndex = new Dictionary<string, int>();
        var indexToNode = new Dictionary<int, string>();
        int index = 0;

        foreach (var node in graph.Vertices)
        {
            nodeToIndex[node] = index;
            indexToNode[index] = node;
            index++;
        }

        int nodeCount = graph.VertexCount;
        var pageRank = new double[nodeCount];
        var newPageRank = new double[nodeCount];

        // Initialize PageRank values
        double initialValue = 1.0 / nodeCount;
        for (int i = 0; i < nodeCount; i++)
        {
            pageRank[i] = initialValue;
        }

        // Calculate PageRank
        bool converged = false;
        int iteration = 0;

        while (!converged && iteration < maxIterations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Reset new PageRank values
            for (int i = 0; i < nodeCount; i++)
            {
                newPageRank[i] = (1 - dampingFactor) / nodeCount;
            }

            // Update PageRank values
            foreach (var node in graph.Vertices)
            {
                int nodeIdx = nodeToIndex[node];
                var outEdges = graph.OutEdges(node).ToList();

                if (outEdges.Count > 0)
                {
                    double contribution = pageRank[nodeIdx] * dampingFactor / outEdges.Count;

                    foreach (var edge in outEdges)
                    {
                        int targetIdx = nodeToIndex[edge.Target];
                        newPageRank[targetIdx] += contribution;
                    }
                }
                else
                {
                    // Distribute to all nodes if no outgoing edges
                    double contribution = pageRank[nodeIdx] * dampingFactor / nodeCount;

                    for (int i = 0; i < nodeCount; i++)
                    {
                        newPageRank[i] += contribution;
                    }
                }
            }

            // Check for convergence
            double diff = 0;
            for (int i = 0; i < nodeCount; i++)
            {
                diff += Math.Abs(newPageRank[i] - pageRank[i]);
            }

            converged = diff < convergenceThreshold;

            // Update PageRank values
            for (int i = 0; i < nodeCount; i++)
            {
                pageRank[i] = newPageRank[i];
            }

            iteration++;
        }

        // Convert to float array
        var embeddings = new float[nodeCount];
        for (int i = 0; i < nodeCount; i++)
        {
            embeddings[i] = (float)pageRank[i];
        }

        return embeddings;
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
            return default(T);
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
        _logger.LogInformation("Finding shortest path from {SourceId} to {TargetId}", sourceEntityId, targetEntityId);

        // Use proper AGE Cypher syntax for finding shortest path
        var query = $@"SELECT * FROM ag_catalog.cypher('{_graphName}', $$
            MATCH p = shortestPath((source:Entity)-[*]->(target:Entity))
            WHERE source.id = '{sourceEntityId}' AND target.id = '{targetEntityId}'
            RETURN [r IN relationships(p) | {{source_id: startNode(r).id, target_id: endNode(r).id, type: type(r), properties: properties(r)}}] AS path
        $$) as (path agtype);";

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
        _logger.LogInformation("Getting nodes related to keyword: {Keyword}", keyword);

        // Use proper AGE Cypher syntax for finding related nodes
        var query = $@"SELECT * FROM ag_catalog.cypher('{_graphName}', $$
            MATCH (n:Entity)
            WHERE n.name =~ '(?i).*{keyword}.*' OR n.description =~ '(?i).*{keyword}.*'
            OPTIONAL MATCH (n)-[r]-(related)
            RETURN n, collect(r) as rels, collect(related) as related_nodes
        $$) as (n agtype, rels agtype, related_nodes agtype);";

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

    public async Task<GraphData> GetGraphDataAsync(
        string label = "*",
        int maxDepth = 2,
        int maxNodes = 100,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting graph data with label: {Label}, maxDepth: {MaxDepth}, maxNodes: {MaxNodes}",
            label, maxDepth, maxNodes);

        try
        {
            // First try using the AGE Cypher approach
            return await GetGraphDataWithAGEAsync(label, maxDepth, maxNodes, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error using AGE Cypher for getting graph data. Falling back to direct SQL approach.");

            // Fallback to a more direct SQL approach
            return await GetGraphDataWithSQLAsync(label, maxDepth, maxNodes, cancellationToken);
        }
    }

    private async Task<GraphData> GetGraphDataWithAGEAsync(
        string label = "*",
        int maxDepth = 2,
        int maxNodes = 100,
        CancellationToken cancellationToken = default)
    {
        var result = new GraphData();

        // Ensure connection is open
        if (_connection.State != System.Data.ConnectionState.Open)
        {
            await _connection.OpenAsync(cancellationToken);
        }

        // Get entities (nodes) using proper AGE Cypher syntax
        string labelFilter = label != "*" ? $"WHERE n.type = '{label}'" : "";
        var entitiesQuery = $@"SELECT * FROM ag_catalog.cypher('{_graphName}', $$
            MATCH (n:Entity)
            {labelFilter}
            RETURN n
            LIMIT {maxNodes}
        $$) as (n agtype);";

        var entityIds = new HashSet<string>();

        using (var cmd = new NpgsqlCommand(entitiesQuery, _connection))
        using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                var id = reader.GetString(0);
                var propsJson = reader.GetString(1);
                var props = JsonSerializer.Deserialize<Dictionary<string, object>>(propsJson) ?? new Dictionary<string, object>();

                var entity = new GraphEntity
                {
                    Id = Guid.Parse(id),
                    Name = props.TryGetValue("name", out var name) ? name.ToString() : "Unknown",
                    Type = props.TryGetValue("type", out var type) ? type.ToString() : "Unknown",
                    Description = props.TryGetValue("description", out var desc) ? desc.ToString() : "",
                    Keywords = props.TryGetValue("keywords", out var keywords) ?
                        JsonSerializer.Deserialize<List<string>>(keywords.ToString()) : new List<string>(),
                    Weight = props.TryGetValue("weight", out var weight) ?
                        float.Parse(weight.ToString()) : 1.0f
                };

                result.Entities.Add(entity);
                entityIds.Add(id);
            }
        }

        // Get relationships (edges) between the entities
        if (entityIds.Any())
        {
            // Create a comma-separated list of entity IDs for the query
            string entityIdList = string.Join(", ", entityIds.Select(id => $"'{id}'"));

            // Get relationships (edges) using proper AGE Cypher syntax
            var edgesQuery = $@"SELECT * FROM ag_catalog.cypher('{_graphName}', $$
                MATCH (a:Entity)-[r]->(b:Entity)
                WHERE a.id IN [{entityIdList}] AND b.id IN [{entityIdList}]
                RETURN a.id as source_id, b.id as target_id, type(r) as relationship_type, r as relationship_data
            $$) as (source_id agtype, target_id agtype, relationship_type agtype, relationship_data agtype);";
            using (var cmd = new NpgsqlCommand(edgesQuery, _connection))
            using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    var sourceId = reader.GetString(0);
                    var targetId = reader.GetString(1);
                    var relType = reader.GetString(2);
                    var propsJson = reader.GetString(3);
                    var props = JsonSerializer.Deserialize<Dictionary<string, object>>(propsJson) ?? new Dictionary<string, object>();

                    var relationship = new Relationship
                    {
                        Id = Guid.NewGuid(), // Generate a new ID for the relationship
                        SourceEntityId = sourceId,
                        TargetEntityId = targetId,
                        Type = relType,
                        Description = props.TryGetValue("description", out var desc) ? desc.ToString() : "",
                        Keywords = props.TryGetValue("keywords", out var keywords) ?
                            JsonSerializer.Deserialize<List<string>>(keywords.ToString()) : new List<string>(),
                        Weight = props.TryGetValue("weight", out var weight) ?
                            float.Parse(weight.ToString()) : 1.0f
                    };

                    result.Relationships.Add(relationship);
                }
            }
        }

        return result;
    }

    private async Task<GraphData> GetGraphDataWithSQLAsync(
        string label = "*",
        int maxDepth = 2,
        int maxNodes = 100,
        CancellationToken cancellationToken = default)
    {
        var result = new GraphData();

        // Ensure connection is open
        if (_connection.State != System.Data.ConnectionState.Open)
        {
            await _connection.OpenAsync(cancellationToken);
        }

        // Get entities (nodes) using direct SQL
        string labelFilter = label != "*" ? $"AND properties->>'type' = '{label}'" : "";
        var entitiesQuery = $@"SELECT id, properties FROM ag_catalog.ag_vertex
            WHERE graph_name = '{_graphName}' {labelFilter}
            LIMIT {maxNodes}";

        var entityIds = new HashSet<string>();

        using (var cmd = new NpgsqlCommand(entitiesQuery, _connection))
        using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                var id = reader.GetString(0);
                var propsJson = reader.GetString(1);
                var props = JsonSerializer.Deserialize<Dictionary<string, object>>(propsJson) ?? new Dictionary<string, object>();

                var entity = new GraphEntity
                {
                    Id = Guid.Parse(id),
                    Name = props.TryGetValue("name", out var name) ? name.ToString() : "Unknown",
                    Type = props.TryGetValue("type", out var type) ? type.ToString() : "Unknown",
                    Description = props.TryGetValue("description", out var desc) ? desc.ToString() : "",
                    Keywords = props.TryGetValue("keywords", out var keywords) ?
                        JsonSerializer.Deserialize<List<string>>(keywords.ToString()) : new List<string>(),
                    Weight = props.TryGetValue("weight", out var weight) ?
                        float.Parse(weight.ToString()) : 1.0f
                };

                result.Entities.Add(entity);
                entityIds.Add(id);
            }
        }

        // Get relationships (edges) between the entities
        if (entityIds.Any())
        {
            // Create a comma-separated list of entity IDs for the query
            string entityIdList = string.Join(", ", entityIds.Select(id => $"'{id}'"));

            // Get relationships (edges) using direct SQL
            var edgesQuery = $@"SELECT start_id, end_id, label, properties FROM ag_catalog.ag_edge
                WHERE graph_name = '{_graphName}'
                AND start_id IN ({entityIdList})
                AND end_id IN ({entityIdList})";

            using (var cmd = new NpgsqlCommand(edgesQuery, _connection))
            using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    var sourceId = reader.GetString(0);
                    var targetId = reader.GetString(1);
                    var relType = reader.GetString(2);
                    var propsJson = reader.GetString(3);
                    var props = JsonSerializer.Deserialize<Dictionary<string, object>>(propsJson) ?? new Dictionary<string, object>();

                    var relationship = new Relationship
                    {
                        Id = Guid.NewGuid(), // Generate a new ID for the relationship
                        SourceEntityId = sourceId,
                        TargetEntityId = targetId,
                        Type = relType,
                        Description = props.TryGetValue("description", out var desc) ? desc.ToString() : "",
                        Keywords = props.TryGetValue("keywords", out var keywords) ?
                            JsonSerializer.Deserialize<List<string>>(keywords.ToString()) : new List<string>(),
                        Weight = props.TryGetValue("weight", out var weight) ?
                            float.Parse(weight.ToString()) : 1.0f
                    };

                    result.Relationships.Add(relationship);
                }
            }
        }

        return result;
    }

    public async Task<IEnumerable<string>> GetLabelsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting graph labels");

        var labels = new HashSet<string>();

        // Ensure connection is open
        if (_connection.State != System.Data.ConnectionState.Open)
        {
            await _connection.OpenAsync(cancellationToken);
        }

        try
        {
            // Use proper AGE Cypher syntax for getting distinct labels
            var query = $@"SELECT * FROM ag_catalog.cypher('{_graphName}', $$
                MATCH (n:Entity)
                RETURN DISTINCT n.type as label
            $$) as (label agtype);";

            using (var cmd = new NpgsqlCommand(query, _connection))
            using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    if (!reader.IsDBNull(0))
                    {
                        labels.Add(reader.GetString(0));
                    }
                }
            }

            return labels;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting graph labels");
            throw;
        }
    }

    public async Task<GraphEntity> GetEntityAsync(string id, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting entity with ID: {Id}", id);

        // Ensure connection is open
        if (_connection.State != System.Data.ConnectionState.Open)
        {
            await _connection.OpenAsync(cancellationToken);
        }

        try
        {
            // Use proper AGE Cypher syntax for getting an entity by ID
            var query = $@"SELECT * FROM ag_catalog.cypher('{_graphName}', $$
                MATCH (n:Entity)
                WHERE n.id = '{id}'
                RETURN n
            $$) as (n agtype);";

            using (var cmd = new NpgsqlCommand(query, _connection))
            using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
            {
                if (await reader.ReadAsync(cancellationToken))
                {
                    var entityId = reader.GetString(0);
                    var propsJson = reader.GetString(1);
                    var props = JsonSerializer.Deserialize<Dictionary<string, object>>(propsJson) ?? new Dictionary<string, object>();

                    return new GraphEntity
                    {
                        Id = Guid.Parse(entityId),
                        Name = props.TryGetValue("name", out var name) ? name.ToString() : "Unknown",
                        Type = props.TryGetValue("type", out var type) ? type.ToString() : "Unknown",
                        Description = props.TryGetValue("description", out var desc) ? desc.ToString() : "",
                        Keywords = props.TryGetValue("keywords", out var keywords) ?
                            JsonSerializer.Deserialize<List<string>>(keywords.ToString()) : new List<string>(),
                        Weight = props.TryGetValue("weight", out var weight) ?
                            float.Parse(weight.ToString()) : 1.0f
                    };
                }

                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting entity with ID: {Id}", id);
            throw;
        }
    }

    public async Task<IEnumerable<Relationship>> GetEntityRelationshipsAsync(string id, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting relationships for entity with ID: {Id}", id);

        var relationships = new List<Relationship>();

        // Ensure connection is open
        if (_connection.State != System.Data.ConnectionState.Open)
        {
            await _connection.OpenAsync(cancellationToken);
        }

        try
        {
            // Use proper AGE Cypher syntax for getting entity relationships
            var query = $@"SELECT * FROM ag_catalog.cypher('{_graphName}', $$
                MATCH (n:Entity)-[r]-(m:Entity)
                WHERE n.id = '{id}'
                RETURN n.id as source_id, m.id as target_id, type(r) as relationship_type, r as relationship_data
            $$) as (source_id agtype, target_id agtype, relationship_type agtype, relationship_data agtype);";

            using (var cmd = new NpgsqlCommand(query, _connection))
            using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    var sourceId = reader.GetString(0);
                    var targetId = reader.GetString(1);
                    var relType = reader.GetString(2);
                    var propsJson = reader.GetString(3);
                    var props = JsonSerializer.Deserialize<Dictionary<string, object>>(propsJson) ?? new Dictionary<string, object>();

                    var relationship = new Relationship
                    {
                        Id = Guid.NewGuid(), // Generate a new ID for the relationship
                        SourceEntityId = sourceId,
                        TargetEntityId = targetId,
                        Type = relType,
                        Description = props.TryGetValue("description", out var desc) ? desc.ToString() : "",
                        Keywords = props.TryGetValue("keywords", out var keywords) ?
                            JsonSerializer.Deserialize<List<string>>(keywords.ToString()) : new List<string>(),
                        Weight = props.TryGetValue("weight", out var weight) ?
                            float.Parse(weight.ToString()) : 1.0f
                    };

                    relationships.Add(relationship);
                }
            }

            return relationships;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting relationships for entity with ID: {Id}", id);
            throw;
        }
    }

    public async Task<GraphEntity> UpdateEntityAsync(GraphEntity entity, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating entity with ID: {Id}", entity.Id);

        // Ensure connection is open
        if (_connection.State != System.Data.ConnectionState.Open)
        {
            await _connection.OpenAsync(cancellationToken);
        }

        try
        {
            // Escape single quotes in string properties
            var name = entity.Name.Replace("'", "''");
            var type = entity.Type.Replace("'", "''");
            var description = entity.Description.Replace("'", "''");

            // Create properties object with all entity properties
            var propertiesJson = JsonSerializer.Serialize(new
            {
                id = entity.Id.ToString(),
                name,
                type,
                description,
                keywords = entity.Keywords,
                weight = entity.Weight,
                sourceId = entity.SourceId,
                vectorStoreId = entity.VectorStoreId.ToString(),
                createdAt = entity.CreatedAt
            });

            // Use proper AGE Cypher syntax for updating an entity
            var query = $@"SELECT * FROM ag_catalog.cypher('{_graphName}', $$
                MATCH (n:Entity)
                WHERE n.id = '{entity.Id}'
                SET n = {propertiesJson}
                RETURN n
            $$) as (n agtype);";

            using (var cmd = new NpgsqlCommand(query, _connection))
            {
                var rowsAffected = await cmd.ExecuteNonQueryAsync(cancellationToken);

                if (rowsAffected > 0)
                {
                    return entity;
                }

                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating entity with ID: {Id}", entity.Id);
            throw;
        }
    }

    public async Task<Relationship> UpdateRelationshipAsync(Relationship relationship, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating relationship with ID: {Id}", relationship.Id);

        // Ensure connection is open
        if (_connection.State != System.Data.ConnectionState.Open)
        {
            await _connection.OpenAsync(cancellationToken);
        }

        try
        {
            // Escape single quotes in string properties
            var type = relationship.Type.Replace("'", "''");
            var description = relationship.Description.Replace("'", "''");

            // Create properties object with all relationship properties
            var propertiesJson = JsonSerializer.Serialize(new
            {
                id = relationship.Id.ToString(),
                description,
                weight = relationship.Weight,
                keywords = relationship.Keywords,
                sourceId = relationship.SourceId,
                vectorStoreId = relationship.VectorStoreId.ToString(),
                createdAt = relationship.CreatedAt
            });

            // Use proper AGE Cypher syntax for updating a relationship
            var query = $@"SELECT * FROM ag_catalog.cypher('{_graphName}', $$
                MATCH (a:Entity)-[r:{type}]->(b:Entity)
                WHERE a.id = '{relationship.SourceEntityId}' AND b.id = '{relationship.TargetEntityId}'
                SET r = {propertiesJson}
                RETURN r
            $$) as (r agtype);";

            using (var cmd = new NpgsqlCommand(query, _connection))
            {
                var rowsAffected = await cmd.ExecuteNonQueryAsync(cancellationToken);

                if (rowsAffected > 0)
                {
                    return relationship;
                }

                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating relationship with ID: {Id}", relationship.Id);
            throw;
        }
    }

    public async Task<IDictionary<string, float>> RunPageRankAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Running PageRank algorithm");

        // Get all nodes and edges to build the graph using proper AGE Cypher syntax
        var nodesQuery = $@"SELECT * FROM ag_catalog.cypher('{_graphName}', $$
            MATCH (n:Entity)
            RETURN n.id as id, n as properties
        $$) as (id agtype, properties agtype);";

        var edgesQuery = $@"SELECT * FROM ag_catalog.cypher('{_graphName}', $$
            MATCH (a:Entity)-[r]->(b:Entity)
            RETURN a.id as source_id, b.id as target_id, r as properties
        $$) as (source_id agtype, target_id agtype, properties agtype);";

        // Ensure connection is open
        if (_connection.State != System.Data.ConnectionState.Open)
        {
            await _connection.OpenAsync(cancellationToken);
        }

        try
        {
            // Build a graph using QuikGraph
            var graph = new QuikGraph.AdjacencyGraph<string, QuikGraph.Edge<string>>(true);

            // Get all nodes
            using (var cmd = new NpgsqlCommand(nodesQuery, _connection))
            using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    var id = reader.GetString(0);
                    graph.AddVertex(id);
                }
            }

            // Get all edges
            using (var cmd = new NpgsqlCommand(edgesQuery, _connection))
            using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    var sourceId = reader.GetString(0);
                    var targetId = reader.GetString(1);
                    var edge = new QuikGraph.Edge<string>(sourceId, targetId);
                    graph.AddEdge(edge);
                }
            }

            // Calculate PageRank
            return await CalculatePageRankAsync(graph, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running PageRank algorithm");
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
