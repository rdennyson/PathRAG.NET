using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using PathRAG.Core.Models;
using PathRAG.Infrastructure.Data;
using System.Text;

namespace PathRAG.Core.Services.Vector;

public class VectorSearchService : IVectorSearchService
{
    private readonly PathRagDbContext _dbContext;
    private readonly PathRagOptions _options;
    private readonly string _connectionString;
    private readonly ILogger<VectorSearchService> _logger;

    public VectorSearchService(
        PathRagDbContext dbContext,
        IOptions<PathRagOptions> options,
        IConfiguration configuration,
        ILogger<VectorSearchService> logger)
    {
        _dbContext = dbContext;
        _options = options.Value;
        _connectionString = configuration.GetConnectionString("DefaultConnection");
        _logger = logger;
    }

    public async Task<IReadOnlyList<TextChunk>> SearchTextChunksAsync(
        float[] queryEmbedding,
        int topK,
        CancellationToken cancellationToken = default)
    {
        return await PerformVectorSearchAsync<TextChunk>(
            "TextChunks",
            queryEmbedding,
            topK,
            cancellationToken);
    }

    public async Task<IList<GraphEntity>> SearchEntitiesAsync(
        float[] queryEmbedding,
        int topK,
        CancellationToken cancellationToken = default)
    {
        return await PerformVectorSearchAsync<GraphEntity>(
            "Entities",
            queryEmbedding,
            topK,
            cancellationToken);
    }

    public async Task<IReadOnlyList<Relationship>> SearchRelationshipsAsync(
        float[] queryEmbedding,
        int topK,
        CancellationToken cancellationToken = default)
    {
        return await PerformVectorSearchAsync<Relationship>(
            "Relationships",
            queryEmbedding,
            topK,
            cancellationToken);
    }

    private async Task<List<T>> PerformVectorSearchAsync<T>(
        string tableName,
        float[] queryEmbedding,
        int topK,
        CancellationToken cancellationToken) where T : class, new()
    {
        var results = new List<T>();

        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            // Convert embedding to string format for PostgreSQL
            var embeddingStr = new StringBuilder("[");
            for (int i = 0; i < queryEmbedding.Length; i++)
            {
                embeddingStr.Append(queryEmbedding[i].ToString("G"));
                if (i < queryEmbedding.Length - 1)
                    embeddingStr.Append(',');
            }
            embeddingStr.Append("]");

            // Use pgvector's cosine distance operator <=> for similarity search
            var sql = $@"SELECT * FROM ""{tableName}"" ORDER BY embedding <=> '{embeddingStr}'::vector LIMIT {topK}";

            await using var cmd = new NpgsqlCommand(sql, connection);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                var item = new T();
                
                // Use reflection to set properties
                var properties = typeof(T).GetProperties();
                foreach (var property in properties)
                {
                    try
                    {
                        var ordinal = reader.GetOrdinal(property.Name);
                        if (!reader.IsDBNull(ordinal))
                        {
                            if (property.Name == "Embedding")
                            {
                                // Handle embedding specially
                                var embeddingBytes = (byte[])reader[ordinal];
                                var embedding = new float[embeddingBytes.Length / sizeof(float)];
                                Buffer.BlockCopy(embeddingBytes, 0, embedding, 0, embeddingBytes.Length);
                                property.SetValue(item, embedding);
                            }
                            else if (property.PropertyType == typeof(Guid))
                            {
                                property.SetValue(item, reader.GetGuid(ordinal));
                            }
                            else if (property.PropertyType == typeof(string))
                            {
                                property.SetValue(item, reader.GetString(ordinal));
                            }
                            else if (property.PropertyType == typeof(int))
                            {
                                property.SetValue(item, reader.GetInt32(ordinal));
                            }
                            else if (property.PropertyType == typeof(float))
                            {
                                property.SetValue(item, reader.GetFloat(ordinal));
                            }
                            else if (property.PropertyType == typeof(DateTime))
                            {
                                property.SetValue(item, reader.GetDateTime(ordinal));
                            }
                            else if (property.PropertyType == typeof(List<string>))
                            {
                                // Handle string arrays
                                var array = reader.GetValue(ordinal) as string[];
                                if (array != null)
                                {
                                    property.SetValue(item, array.ToList());
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error setting property {PropertyName} for type {TypeName}", 
                            property.Name, typeof(T).Name);
                    }
                }

                results.Add(item);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Vector search error for type {TypeName}", typeof(T).Name);
            
            // Fallback to EF Core with in-memory cosine similarity
            if (typeof(T) == typeof(TextChunk))
            {
                var chunks = await _dbContext.TextChunks
                    .OrderByDescending(c => CosineSimilarity(c.Embedding, queryEmbedding))
                    .Take(topK)
                    .ToListAsync(cancellationToken);
                    
                results.AddRange(chunks as IEnumerable<T>);
            }
            else if (typeof(T) == typeof(GraphEntity))
            {
                var entities = await _dbContext.Entities
                    .OrderByDescending(e => CosineSimilarity(e.Embedding, queryEmbedding))
                    .Take(topK)
                    .ToListAsync(cancellationToken);
                    
                results.AddRange(entities as IEnumerable<T>);
            }
            else if (typeof(T) == typeof(Relationship))
            {
                var relationships = await _dbContext.Relationships
                    .OrderByDescending(r => CosineSimilarity(r.Embedding, queryEmbedding))
                    .Take(topK)
                    .ToListAsync(cancellationToken);
                    
                results.AddRange(relationships as IEnumerable<T>);
            }
        }

        return results;
    }
    
    private static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length == 0 || b.Length == 0 || a.Length != b.Length)
            return 0;
            
        float dotProduct = 0;
        float normA = 0;
        float normB = 0;

        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        return dotProduct / (float)(Math.Sqrt(normA) * Math.Sqrt(normB));
    }
}
