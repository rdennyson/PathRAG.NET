using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PathRAG.Core.Models;
using PathRAG.Core.Services.Entity;
using PathRAG.Core.Services.Graph;
using PathRAG.Infrastructure.Data;
using System.Text;
using Npgsql;
using Microsoft.Extensions.Configuration;

namespace PathRAG.Core.Services.Query;

public class HybridQueryService : IHybridQueryService
{
    private readonly PathRagDbContext _dbContext;
    private readonly IGraphStorageService _graphStorage;
    private readonly IEntityExtractionService _entityExtractor;
    private readonly PathRagOptions _options;
    private readonly string _connectionString;

    public HybridQueryService(
        PathRagDbContext dbContext,
        IGraphStorageService graphStorage,
        IEntityExtractionService entityExtractor,
        IOptions<PathRagOptions> options,
        IConfiguration configuration)
    {
        _dbContext = dbContext;
        _graphStorage = graphStorage;
        _entityExtractor = entityExtractor;
        _options = options.Value;
        _connectionString = configuration.GetConnectionString("DefaultConnection");
    }

    public async Task<SearchResult> SearchAsync(
        float[] queryEmbedding,
        IReadOnlyList<string> highLevelKeywords,
        IReadOnlyList<string> lowLevelKeywords,
        CancellationToken cancellationToken = default)
    {
        // Semantic search using embeddings with pgvector
        var semanticChunks = await PerformVectorSearchAsync(
            "TextChunks",
            queryEmbedding,
            _options.TopK / 2,
            cancellationToken);

        // Fallback to in-memory cosine similarity if pgvector search fails
        if (semanticChunks.Count == 0)
        {
            semanticChunks = await _dbContext.TextChunks
                .OrderByDescending(c => CosineSimilarity(c.Embedding, queryEmbedding))
                .Take(_options.TopK / 2)
                .ToListAsync(cancellationToken);
        }

        // Keyword-based search
        var keywordChunks = await _dbContext.TextChunks
            .Where(c => lowLevelKeywords.Any(k => c.Content.Contains(k)))
            .ToListAsync(cancellationToken);

        // Graph-based search using high-level keywords
        var entities = new List<GraphEntity>();
        var relationships = new List<Relationship>();

        foreach (var keyword in highLevelKeywords)
        {
            var relatedNodes = await _graphStorage.GetRelatedNodesAsync(keyword);
            entities.AddRange(relatedNodes.OfType<GraphEntity>());
            relationships.AddRange(relatedNodes.OfType<Relationship>());
        }

        // Combine and deduplicate results
        var allChunks = semanticChunks.Union(keywordChunks).Distinct().ToList();

        return new SearchResult(allChunks, entities, relationships);
    }

    private static float CosineSimilarity(float[] a, float[] b)
    {
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

    private async Task<List<TextChunk>> PerformVectorSearchAsync(
        string tableName,
        float[] queryEmbedding,
        int topK,
        CancellationToken cancellationToken)
    {
        var results = new List<TextChunk>();

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
            var sql = $@"SELECT * FROM \""{tableName}\"" ORDER BY embedding <=> '{embeddingStr}'::vector LIMIT {topK}";

            await using var cmd = new NpgsqlCommand(sql, connection);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                var chunk = new TextChunk
                {
                    Id = reader.GetGuid(reader.GetOrdinal("Id")),
                    Content = reader.GetString(reader.GetOrdinal("Content")),
                    TokenCount = reader.GetInt32(reader.GetOrdinal("TokenCount")),
                    FullDocumentId = reader.GetString(reader.GetOrdinal("FullDocumentId")),
                    ChunkOrderIndex = reader.GetInt32(reader.GetOrdinal("ChunkOrderIndex")),
                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt"))
                };

                // Get embedding as array
                var embeddingBytes = (byte[])reader[reader.GetOrdinal("Embedding")];
                chunk.Embedding = new float[embeddingBytes.Length / sizeof(float)];
                Buffer.BlockCopy(embeddingBytes, 0, chunk.Embedding, 0, embeddingBytes.Length);

                results.Add(chunk);
            }
        }
        catch (Exception ex)
        {
            // Log the exception and fall back to EF Core query
            Console.WriteLine($"Vector search error: {ex.Message}");
        }

        return results;
    }
}