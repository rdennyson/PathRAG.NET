using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using PathRAG.Core.Models;
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
        // The graph name defined elsewhere in your class.
        var query = $@"
            DO
            $$
            BEGIN
                -- Check if the graph with the specified name already exists.
                IF NOT EXISTS (SELECT 1 FROM ag_catalog.ag_graph WHERE name = '{_graphName}') THEN
                    -- Create the graph using Apache AGE's create_graph function.
                    PERFORM create_graph('{_graphName}');
                END IF;
            END
            $$;
        ";
        // Execute the query. You can adjust the generic type based on your implementation of ExecuteCypherQuery.
        await ExecuteCypherQuery<object>(query, cancellationToken);
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

    public async Task<GraphData> GetGraphDataAsync(
        string label = "*",
        int maxDepth = 2,
        int maxNodes = 100,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting graph data with label: {Label}, maxDepth: {MaxDepth}, maxNodes: {MaxNodes}",
            label, maxDepth, maxNodes);

        var result = new GraphData();

        // Ensure connection is open
        if (_connection.State != System.Data.ConnectionState.Open)
        {
            await _connection.OpenAsync(cancellationToken);
        }

        try
        {
            // Get entities (nodes)
            var entitiesQuery = $"SELECT id, properties FROM ag_catalog.ag_vertex WHERE graph_name = '{_graphName}'";
            if (label != "*")
            {
                entitiesQuery += $" AND properties->>'type' = '{label}'";
            }
            entitiesQuery += $" LIMIT {maxNodes}";

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
                var edgesQuery = $"SELECT start_id, end_id, label, properties FROM ag_catalog.ag_edge " +
                                $"WHERE graph_name = '{_graphName}' " +
                                $"AND start_id IN ({string.Join(", ", entityIds.Select(id => $"'{id}'"))}) " +
                                $"AND end_id IN ({string.Join(", ", entityIds.Select(id => $"'{id}'"))})";

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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting graph data");
            throw;
        }
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
            var query = $"SELECT DISTINCT properties->>'type' FROM ag_catalog.ag_vertex WHERE graph_name = '{_graphName}'";

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
            var query = $"SELECT id, properties FROM ag_catalog.ag_vertex WHERE graph_name = '{_graphName}' AND id = '{id}'";

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
            var query = $"SELECT start_id, end_id, label, properties FROM ag_catalog.ag_edge " +
                        $"WHERE graph_name = '{_graphName}' AND (start_id = '{id}' OR end_id = '{id}')";

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
            var props = new Dictionary<string, object>
            {
                { "name", entity.Name },
                { "type", entity.Type },
                { "description", entity.Description },
                { "keywords", entity.Keywords },
                { "weight", entity.Weight }
            };

            var propsJson = JsonSerializer.Serialize(props);
            var query = $"UPDATE ag_catalog.ag_vertex SET properties = '{propsJson}' " +
                        $"WHERE graph_name = '{_graphName}' AND id = '{entity.Id}'";

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
            var props = new Dictionary<string, object>
            {
                { "description", relationship.Description },
                { "keywords", relationship.Keywords },
                { "weight", relationship.Weight }
            };

            var propsJson = JsonSerializer.Serialize(props);
            var query = $"UPDATE ag_catalog.ag_edge SET properties = '{propsJson}' " +
                        $"WHERE graph_name = '{_graphName}' AND start_id = '{relationship.SourceEntityId}' " +
                        $"AND end_id = '{relationship.TargetEntityId}'";

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