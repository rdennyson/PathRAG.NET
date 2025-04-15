using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using PathRAG.Core.Models;
using PathRAG.Core.Services.Entity;
using PathRAG.Core.Services.Graph;
using PathRAG.Core.Services.Vector;
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
    private readonly IVectorSearchService _vectorSearchService;
    private readonly PathRagOptions _options;
    private readonly string _connectionString;
    private readonly ILogger<HybridQueryService> _logger;

    public HybridQueryService(
        PathRagDbContext dbContext,
        IGraphStorageService graphStorage,
        IEntityExtractionService entityExtractor,
        IVectorSearchService vectorSearchService,
        IOptions<PathRagOptions> options,
        IConfiguration configuration,
        ILogger<HybridQueryService> logger)
    {
        _dbContext = dbContext;
        _graphStorage = graphStorage;
        _entityExtractor = entityExtractor;
        _vectorSearchService = vectorSearchService;
        _options = options.Value;
        _connectionString = configuration.GetConnectionString("DefaultConnection");
        _logger = logger;
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
            _logger.LogError(ex, "Vector search error");
        }

        return results;
    }

    public async Task<List<TextChunk>> SemanticSearchAsync(
        float[] queryEmbedding,
        List<Guid> vectorStoreIds,
        int topK,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Perform vector similarity search on text chunks
            var chunks = await _vectorSearchService.SearchTextChunksAsync(
                queryEmbedding,
                topK,
                cancellationToken);

            // Filter by vector store IDs
            return chunks
                .Where(c => vectorStoreIds.Contains(c.VectorStoreId))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing semantic search");
            return new List<TextChunk>();
        }
    }

    public async Task<List<TextChunk>> HybridSearchAsync(
        string query,
        float[] queryEmbedding,
        List<Guid> vectorStoreIds,
        int topK,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Perform vector similarity search
            var vectorResults = await _vectorSearchService.SearchTextChunksAsync(
                queryEmbedding,
                topK,
                cancellationToken);

            // Perform keyword search
            var keywordResults = await _dbContext.TextChunks
                .Where(c => vectorStoreIds.Contains(c.VectorStoreId))
                .Where(c => EF.Functions.ILike(c.Content, $"%{query}%"))
                .Take(topK)
                .ToListAsync(cancellationToken);

            // Combine results, prioritizing exact matches
            var combinedResults = new List<TextChunk>();
            combinedResults.AddRange(keywordResults);

            foreach (var chunk in vectorResults)
            {
                if (!combinedResults.Any(c => c.Id == chunk.Id) && vectorStoreIds.Contains(chunk.VectorStoreId))
                {
                    combinedResults.Add(chunk);
                }
            }

            // Return top K results
            return combinedResults.Take(topK).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing hybrid search");
            return new List<TextChunk>();
        }
    }

    public async Task<(List<TextChunk> chunks, List<GraphEntity> entities, List<Relationship> relationships)> GraphSearchAsync(
        string query,
        float[] queryEmbedding,
        List<Guid> vectorStoreIds,
        int topK,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get text chunks using hybrid search
            var chunks = await HybridSearchAsync(
                query,
                queryEmbedding,
                vectorStoreIds,
                topK,
                cancellationToken);

            // Get relevant entities
            var entities = await _vectorSearchService.SearchEntitiesAsync(
                queryEmbedding,
                topK,
                cancellationToken);

            // Filter entities by vector store IDs
            entities = entities
                .Where(e => vectorStoreIds.Contains(e.VectorStoreId))
                .ToList();

            // Also search for entities by name
            var keywordEntities = await _dbContext.Entities
                .Where(e => vectorStoreIds.Contains(e.VectorStoreId))
                .Where(e => EF.Functions.ILike(e.Name, $"%{query}%") ||
                           EF.Functions.ILike(e.Description, $"%{query}%"))
                .Take(topK)
                .ToListAsync(cancellationToken);

            // Combine entity results
            foreach (var entity in keywordEntities)
            {
                if (!entities.Any(e => e.Id == entity.Id))
                {
                    entities.Add(entity);
                }
            }

            // Get entity IDs for relationship search
            var entityIds = entities.Select(e => e.Id.ToString()).ToList();

            // Get relationships between entities
            var relationships = await _dbContext.Relationships
                .Where(r => vectorStoreIds.Contains(r.VectorStoreId))
                .Where(r => entityIds.Contains(r.SourceEntityId) || entityIds.Contains(r.TargetEntityId))
                .Take(topK)
                .ToListAsync(cancellationToken);

            return (chunks, entities.ToList(), relationships);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing graph search");
            return (new List<TextChunk>(), new List<GraphEntity>(), new List<Relationship>());
        }
    }
}