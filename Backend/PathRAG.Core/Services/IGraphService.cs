using Npgsql;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace PathRAG.Core.Services;

public interface IGraphService
{
    Task<bool> HasNode(string nodeId);
    Task<bool> HasEdge(string sourceNodeId, string targetNodeId);
    Task<int> GetNodeDegree(string nodeId);
    Task<int> GetEdgeDegree(string sourceNodeId, string targetNodeId);
    Task<float> GetPageRank(string nodeId);
    Task<Dictionary<string, object>> GetNode(string nodeId);
    Task<Dictionary<string, object>> GetEdge(string sourceNodeId, string targetNodeId);
}

public class AgeGraphService : IGraphService
{
    private readonly NpgsqlConnection _connection;
    private readonly string _graphName;

    public AgeGraphService(IConfiguration config)
    {
        _connection = new NpgsqlConnection(config.GetConnectionString("DefaultConnection"));
        _graphName = config["PathRAG:GraphName"] ?? "pathrag";
    }

    public async Task<bool> HasNode(string nodeId)
    {
        var query = $"MATCH (n) WHERE id(n) = '{nodeId}' RETURN COUNT(n) > 0";
        return await ExecuteCypherQuery<bool>(query);
    }

    public async Task<bool> HasEdge(string sourceNodeId, string targetNodeId)
    {
        var query = $"MATCH (s)-[r]->(t) WHERE id(s) = '{sourceNodeId}' AND id(t) = '{targetNodeId}' RETURN COUNT(r) > 0";
        return await ExecuteCypherQuery<bool>(query);
    }

    public async Task<int> GetNodeDegree(string nodeId)
    {
        var query = $"MATCH (n)-[r]-() WHERE id(n) = '{nodeId}' RETURN COUNT(r)";
        return await ExecuteCypherQuery<int>(query);
    }

    public async Task<int> GetEdgeDegree(string sourceNodeId, string targetNodeId)
    {
        var query = $"""
            MATCH (s)-[r1]-() WHERE id(s) = '{sourceNodeId}'
            WITH COUNT(r1) as degree1
            MATCH (t)-[r2]-() WHERE id(t) = '{targetNodeId}'
            RETURN degree1 + COUNT(r2)
            """;
        return await ExecuteCypherQuery<int>(query);
    }

    public async Task<float> GetPageRank(string nodeId)
    {
        var query = $"""
            CALL gds.pageRank.stream('{_graphName}')
            YIELD nodeId, score
            WHERE nodeId = '{nodeId}'
            RETURN score
            """;
        return await ExecuteCypherQuery<float>(query);
    }

    public async Task<Dictionary<string, object>> GetNode(string nodeId)
    {
        var query = $"MATCH (n) WHERE id(n) = '{nodeId}' RETURN properties(n) as props";
        return await ExecuteCypherQuery<Dictionary<string, object>>(query);
    }

    public async Task<Dictionary<string, object>> GetEdge(string sourceNodeId, string targetNodeId)
    {
        var query = $"""
            MATCH (s)-[r]->(t) 
            WHERE id(s) = '{sourceNodeId}' AND id(t) = '{targetNodeId}'
            RETURN properties(r) as props
            """;
        return await ExecuteCypherQuery<Dictionary<string, object>>(query);
    }

    private async Task<T> ExecuteCypherQuery<T>(string query)
    {
        await _connection.OpenAsync();
        try
        {
            using var cmd = new NpgsqlCommand();
            cmd.Connection = _connection;
            cmd.CommandText = $"SELECT * FROM cypher('{_graphName}', $1) as (result agtype)";
            cmd.Parameters.AddWithValue(query);
            
            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return default;

            var result = reader.GetValue(0).ToString();
            if (string.IsNullOrEmpty(result))
                return default;

            // Handle different return types
            if (typeof(T) == typeof(bool))
                return (T)(object)bool.Parse(result);
            
            if (typeof(T) == typeof(int))
                return (T)(object)int.Parse(result);
            
            if (typeof(T) == typeof(float))
                return (T)(object)float.Parse(result);
            
            if (typeof(T) == typeof(Dictionary<string, object>))
                return (T)(object)JsonSerializer.Deserialize<Dictionary<string, object>>(result);

            throw new NotSupportedException($"Unsupported return type: {typeof(T)}");
        }
        finally
        {
            await _connection.CloseAsync();
        }
    }
}
