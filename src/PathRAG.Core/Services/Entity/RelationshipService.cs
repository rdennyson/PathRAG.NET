using Azure.AI.OpenAI;
using Microsoft.Extensions.Options;
using PathRAG.Core.Models;
using PathRAG.Core.Services.Graph;
using PathRAG.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace PathRAG.Core.Services.Entity;

public class RelationshipService : IRelationshipService
{
    private readonly OpenAIClient _openAIClient;
    private readonly PathRagOptions _options;
    private readonly IGraphStorageService _graphStorage;
    private readonly PathRagDbContext _dbContext;
    private readonly ILogger<RelationshipService> _logger;

    public RelationshipService(
        OpenAIClient openAIClient,
        IOptions<PathRagOptions> options,
        IGraphStorageService graphStorage,
        PathRagDbContext dbContext,
        ILogger<RelationshipService> logger)
    {
        _openAIClient = openAIClient;
        _options = options.Value;
        _graphStorage = graphStorage;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<List<Relationship>> ExtractRelationshipsAsync(
        List<GraphEntity> entities,
        string sourceText,
        CancellationToken cancellationToken = default)
    {
        var relationships = new List<Relationship>();

        // Create pairs of entities to analyze
        var entityPairs = GetEntityPairs(entities);

        foreach (var (entity1, entity2) in entityPairs)
        {
            var prompt = new ChatCompletionsOptions
            {
                Messages =
                {  
                    new ChatRequestSystemMessage("Analyze the relationship between these two entities based on the provided text."),
                    new ChatRequestUserMessage($"Text: {sourceText}\n\nEntity 1: {entity1.Name} ({entity1.Type})\nEntity 2: {entity2.Name} ({entity2.Type})")
                },
                Temperature = _options.Temperature,
                MaxTokens = _options.MaxTokens
            };

            var response = await _openAIClient.GetChatCompletionsAsync(
                prompt,
                cancellationToken
            );

            var relationship = ParseRelationshipResponse(
                response.Value.Choices[0].Message.Content,
                entity1.Id.ToString(),
                entity2.Id.ToString()
            );

            if (relationship != null)
            {
                relationships.Add(relationship);
            }
        }

        return relationships;
    }

    public async Task<List<Relationship>> FindRelatedEntitiesAsync(
        string entityId,
        int maxDepth = 2,
        CancellationToken cancellationToken = default)
    {
        var relationships = new List<Relationship>();
        var visited = new HashSet<string> { entityId };
        var queue = new Queue<(string Id, int Depth)>();
        queue.Enqueue((entityId, 0));

        while (queue.Count > 0)
        {
            var (currentId, depth) = queue.Dequeue();

            if (depth >= maxDepth)
                continue;

            var relatedRelationships = await _dbContext.Relationships
                .Where(r => r.SourceEntityId == currentId || r.TargetEntityId == currentId)
                .ToListAsync(cancellationToken);

            foreach (var relationship in relatedRelationships)
            {
                var nextEntityId = relationship.SourceEntityId == currentId
                    ? relationship.TargetEntityId
                    : relationship.SourceEntityId;

                if (!visited.Contains(nextEntityId))
                {
                    visited.Add(nextEntityId);
                    queue.Enqueue((nextEntityId, depth + 1));
                    relationships.Add(relationship);
                }
            }
        }

        return relationships;
    }

    public async Task<float> CalculateRelationshipStrengthAsync(
        string sourceEntityId,
        string targetEntityId,
        CancellationToken cancellationToken = default)
    {
        var directRelationship = await _dbContext.Relationships
            .FirstOrDefaultAsync(r =>
                (r.SourceEntityId == sourceEntityId && r.TargetEntityId == targetEntityId) ||
                (r.SourceEntityId == targetEntityId && r.TargetEntityId == sourceEntityId),
                cancellationToken);

        if (directRelationship != null)
            return directRelationship.Weight;

        // Calculate strength based on graph embeddings
        var sourceEntity = await _dbContext.Entities
            .FirstOrDefaultAsync(e => e.Id.ToString() == sourceEntityId, cancellationToken);
        var targetEntity = await _dbContext.Entities
            .FirstOrDefaultAsync(e => e.Id.ToString() == targetEntityId, cancellationToken);

        if (sourceEntity?.Embedding == null || targetEntity?.Embedding == null)
            return 0;

        return CalculateCosineSimilarity(sourceEntity.Embedding, targetEntity.Embedding);
    }

    public async Task<List<Relationship>> GetRelationshipPathAsync(
        string sourceEntityId,
        string targetEntityId,
        CancellationToken cancellationToken = default)
    {
        return await _graphStorage.FindShortestPathAsync(sourceEntityId, targetEntityId, cancellationToken);
    }

    private IEnumerable<(GraphEntity, GraphEntity)> GetEntityPairs(List<GraphEntity> entities)
    {
        for (int i = 0; i < entities.Count; i++)
        {
            for (int j = i + 1; j < entities.Count; j++)
            {
                yield return (entities[i], entities[j]);
            }
        }
    }

    private Relationship? ParseRelationshipResponse(string response, string sourceId, string targetId)
    {
        try
        {
            // Assume response is in a structured format that can be parsed
            var relationship = new Relationship
            {
                SourceEntityId = sourceId,
                TargetEntityId = targetId,
                Type = ExtractRelationType(response),
                Description = ExtractDescription(response),
                Weight = CalculateWeight(response),
                Keywords = ExtractKeywords(response)
            };

            return relationship.Weight > 0 ? relationship : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse relationship response");
            return null;
        }
    }

    private float CalculateCosineSimilarity(float[] vector1, float[] vector2)
    {
        float dotProduct = 0;
        float norm1 = 0;
        float norm2 = 0;

        for (int i = 0; i < vector1.Length; i++)
        {
            dotProduct += vector1[i] * vector2[i];
            norm1 += vector1[i] * vector1[i];
            norm2 += vector2[i] * vector2[i];
        }

        return dotProduct / (float)(Math.Sqrt(norm1) * Math.Sqrt(norm2));
    }

    private string ExtractRelationType(string response) => ""; // Implementation needed
    private string ExtractDescription(string response) => ""; // Implementation needed
    private float CalculateWeight(string response) => 0; // Implementation needed
    private List<string> ExtractKeywords(string response) => new(); // Implementation needed
}