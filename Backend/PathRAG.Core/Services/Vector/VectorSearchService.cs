using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using PathRAG.Infrastructure.Data;
using PathRAG.Infrastructure.Models;
using Pgvector.EntityFrameworkCore;

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
        CancellationToken cancellationToken = default,
        List<Guid>? vectorStoreIds = null)
    {
        return await PerformVectorSearchAsync<TextChunk>(
            "textchunks",
            queryEmbedding,
            topK,
            cancellationToken,
            vectorStoreIds);
    }

    public async Task<IList<GraphEntity>> SearchEntitiesAsync(
        float[] queryEmbedding,
        int topK,
        CancellationToken cancellationToken = default,
        List<Guid>? vectorStoreIds = null)
    {
        return await PerformVectorSearchAsync<GraphEntity>(
            "entities",
            queryEmbedding,
            topK,
            cancellationToken,
            vectorStoreIds);
    }

    public async Task<IReadOnlyList<Relationship>> SearchRelationshipsAsync(
        float[] queryEmbedding,
        int topK,
        CancellationToken cancellationToken = default,
        List<Guid>? vectorStoreIds = null)
    {
        return await PerformVectorSearchAsync<Relationship>(
            "relationships",
            queryEmbedding,
            topK,
            cancellationToken,
            vectorStoreIds);
    }

    private async Task<List<T>> PerformVectorSearchAsync<T>(
        string tableName,
        float[] queryEmbedding,
        int topK,
        CancellationToken cancellationToken,
        List<Guid>? vectorStoreIds = null) where T : class, new()
    {
        var results = new List<T>();

        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            // Create a Vector from the query embedding
            var queryVector = new Pgvector.Vector(queryEmbedding);

            // Convert to parameter format
            var embeddingStr = queryVector.ToString();

            // Use pgvector's cosine distance operator <=> for similarity search
            string whereClause = "";
            if (vectorStoreIds != null && vectorStoreIds.Count > 0 && (typeof(T) == typeof(TextChunk) || typeof(T) == typeof(GraphEntity) || typeof(T) == typeof(Relationship)))
            {
                string ids = string.Join(",", vectorStoreIds.Select(id => $"'{id}'"));
                whereClause = $" WHERE \"vectorstoreid\" IN ({ids})";
            }

            // Use cosine similarity with vector type
            // Note: The <=> operator is provided by the pgvector extension
            var sql = $@"SELECT * FROM ""{tableName}""{whereClause} ORDER BY embedding <=> '{embeddingStr}'::vector LIMIT {topK}";

            _logger.LogDebug("Executing vector search query: {Sql}", sql);

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
                                if (reader[ordinal] is Pgvector.Vector vector)
                                {
                                    // Handle Vector type directly
                                    property.SetValue(item, vector);
                                }
                                else if (reader[ordinal] is byte[] embeddingBytes)
                                {
                                    try
                                    {
                                        // Convert byte array to float array
                                        var embeddingString = System.Text.Encoding.UTF8.GetString(embeddingBytes);
                                        var floatArray = ParseVectorString(embeddingString);
                                        property.SetValue(item, new Pgvector.Vector(floatArray));
                                    }
                                    catch
                                    {
                                        // Fallback to float array
                                        var embedding = new float[embeddingBytes.Length / sizeof(float)];
                                        Buffer.BlockCopy(embeddingBytes, 0, embedding, 0, embeddingBytes.Length);
                                        property.SetValue(item, new Pgvector.Vector(embedding));
                                    }
                                }
                                else if (reader[ordinal] is float[] floatArray)
                                {
                                    // Handle float array format
                                    property.SetValue(item, new Pgvector.Vector(floatArray));
                                }
                                else
                                {
                                    // Try to parse as string
                                    try
                                    {
                                        var embeddingString = reader[ordinal].ToString();
                                        if (!string.IsNullOrEmpty(embeddingString))
                                        {
                                            // Parse the string to float array
                                            floatArray = ParseVectorString(embeddingString);
                                            property.SetValue(item, new Pgvector.Vector(floatArray));
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogWarning(ex, "Error parsing embedding from database");
                                    }
                                }
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
            _logger.LogError(ex, "Vector search error for type {TypeName}: {ErrorMessage}", typeof(T).Name, ex.Message);

            // Log more details about the error
            if (ex.InnerException != null)
            {
                _logger.LogError("Inner exception: {InnerErrorMessage}", ex.InnerException.Message);
            }

            // Fallback to EF Core with in-memory cosine similarity
            if (typeof(T) == typeof(TextChunk))
            {
                // Fetch chunks with a reasonable limit to avoid loading the entire database
                // We'll fetch more than topK to ensure we have enough for similarity ranking
                int fetchLimit = Math.Min(topK * 10, 1000); // Fetch at most 1000 chunks
                var query = _dbContext.TextChunks.AsQueryable();

                // Apply vector store filter if provided
                if (vectorStoreIds != null && vectorStoreIds.Count > 0)
                {
                    query = query.Where(c => vectorStoreIds.Contains(c.VectorStoreId));
                }

                var chunks = await query.Take(fetchLimit).ToListAsync(cancellationToken);

                // Then calculate similarity and sort in memory
                var sortedChunks = chunks
                    .Select(c => new { Chunk = c, Similarity = c.Embedding.CosineDistance(queryEmbedding) })
                    .OrderByDescending(x => x.Similarity)
                    .Take(topK)
                    .Select(x => x.Chunk)
                    .ToList();

                results.AddRange(sortedChunks as IEnumerable<T>);
            }
            else if (typeof(T) == typeof(GraphEntity))
            {
                // Fetch entities with a reasonable limit
                int fetchLimit = Math.Min(topK * 10, 1000); // Fetch at most 1000 entities
                var query = _dbContext.Entities.AsQueryable();

                // Apply vector store filter if provided
                if (vectorStoreIds != null && vectorStoreIds.Count > 0)
                {
                    query = query.Where(e => vectorStoreIds.Contains(e.VectorStoreId));
                }

                var entities = await query.Take(fetchLimit).ToListAsync(cancellationToken);

                // Then calculate similarity and sort in memory
                var sortedEntities = entities
                    .Select(e => new { Entity = e, Similarity = e.Embedding.CosineDistance(queryEmbedding) })
                    .OrderByDescending(x => x.Similarity)
                    .Take(topK)
                    .Select(x => x.Entity)
                    .ToList();

                results.AddRange(sortedEntities as IEnumerable<T>);
            }
            else if (typeof(T) == typeof(Relationship))
            {
                // Fetch relationships with a reasonable limit
                int fetchLimit = Math.Min(topK * 10, 1000); // Fetch at most 1000 relationships
                var query = _dbContext.Relationships.AsQueryable();

                // Apply vector store filter if provided
                if (vectorStoreIds != null && vectorStoreIds.Count > 0)
                {
                    query = query.Where(r => vectorStoreIds.Contains(r.VectorStoreId));
                }

                var relationships = await query.Take(fetchLimit).ToListAsync(cancellationToken);

                // Then calculate similarity and sort in memory
                var sortedRelationships = relationships
                    .Select(r => new { Relationship = r, Similarity = r.Embedding.CosineDistance(queryEmbedding) })
                    .OrderByDescending(x => x.Similarity)
                    .Take(topK)
                    .Select(x => x.Relationship)
                    .ToList();

                results.AddRange(sortedRelationships as IEnumerable<T>);
            }
        }

        return results;
    }

    // Helper method to parse a vector string like "[1,2,3]" to float[]
    private static float[] ParseVectorString(string vectorString)
    {
        // Remove brackets and split by comma
        vectorString = vectorString.Trim('[', ']');
        var values = vectorString.Split(',');

        var result = new float[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            if (float.TryParse(values[i], out float value))
            {
                result[i] = value;
            }
        }

        return result;
    }
}
