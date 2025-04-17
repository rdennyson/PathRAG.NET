using Azure.AI.OpenAI;
using Microsoft.Extensions.Options;
using PathRAG.Core.Models;
using PathRAG.Core.Services.Graph;
using PathRAG.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pgvector.EntityFrameworkCore;
using PathRAG.Infrastructure.Models;

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
                DeploymentName = _options.CompletionModel,
                Messages =
                {
                    new ChatRequestSystemMessage(@"Analyze the relationship between these two entities based on the provided text.

Your response should include:
1. A clear description of how the entities are related
2. The type of relationship (e.g., 'integrates with', 'sends data to', 'depends on', etc.)
3. The strength of the relationship (on a scale of 0.0 to 1.0, where 1.0 is the strongest)
4. Keywords that characterize the relationship

Format your response as follows:
Description: [Detailed description of the relationship]
Type: [Relationship type]
Strength: [Numeric value between 0.0 and 1.0]
Keywords: [comma-separated list of keywords]

If there is no clear relationship between the entities based on the provided text, respond with 'No relationship found.'
"),
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

    public async Task<double> CalculateRelationshipStrengthAsync(
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

        return sourceEntity.Embedding.CosineDistance(targetEntity.Embedding);
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
            // Check if no relationship was found
            if (response.Contains("No relationship found", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            // Parse the structured response
            string description = "";
            string type = "";
            float weight = 0.5f;
            List<string> keywords = new List<string>();

            // Extract description
            var descriptionMatch = System.Text.RegularExpressions.Regex.Match(response, @"Description:(.+?)(?=Type:|Strength:|Keywords:|$)", System.Text.RegularExpressions.RegexOptions.Singleline);
            if (descriptionMatch.Success)
            {
                description = descriptionMatch.Groups[1].Value.Trim();
            }
            else
            {
                description = ExtractDescription(response);
            }

            // Extract type
            var typeMatch = System.Text.RegularExpressions.Regex.Match(response, @"Type:(.+?)(?=Description:|Strength:|Keywords:|$)", System.Text.RegularExpressions.RegexOptions.Singleline);
            if (typeMatch.Success)
            {
                type = typeMatch.Groups[1].Value.Trim();
            }
            else
            {
                type = ExtractRelationType(response);
            }

            // Extract strength/weight
            var strengthMatch = System.Text.RegularExpressions.Regex.Match(response, @"Strength:(.+?)(?=Description:|Type:|Keywords:|$)", System.Text.RegularExpressions.RegexOptions.Singleline);
            if (strengthMatch.Success && float.TryParse(strengthMatch.Groups[1].Value.Trim(), out float parsedWeight))
            {
                weight = parsedWeight;
            }
            else
            {
                weight = CalculateWeight(response);
            }

            // Extract keywords
            var keywordsMatch = System.Text.RegularExpressions.Regex.Match(response, @"Keywords:(.+?)(?=Description:|Type:|Strength:|$)", System.Text.RegularExpressions.RegexOptions.Singleline);
            if (keywordsMatch.Success)
            {
                var keywordsText = keywordsMatch.Groups[1].Value.Trim();
                keywords = keywordsText.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(k => k.Trim())
                    .Where(k => !string.IsNullOrWhiteSpace(k))
                    .ToList();
            }
            else
            {
                keywords = ExtractKeywords(response);
            }

            // Create the relationship object
            var relationship = new Relationship
            {
                Id = Guid.NewGuid(),
                SourceEntityId = sourceId,
                TargetEntityId = targetId,
                Type = type,
                Description = description,
                Weight = weight,
                Keywords = keywords,
                CreatedAt = DateTime.UtcNow
            };

            return relationship.Weight > 0 ? relationship : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse relationship response: {Response}", response);

            // Fallback to the basic extraction methods
            try
            {
                var relationship = new Relationship
                {
                    Id = Guid.NewGuid(),
                    SourceEntityId = sourceId,
                    TargetEntityId = targetId,
                    Type = ExtractRelationType(response),
                    Description = ExtractDescription(response),
                    Weight = CalculateWeight(response),
                    Keywords = ExtractKeywords(response),
                    CreatedAt = DateTime.UtcNow
                };

                return relationship.Weight > 0 ? relationship : null;
            }
            catch
            {
                return null;
            }
        }
    }

    private string ExtractRelationType(string response)
    {
        try
        {
            // Look for common relationship type indicators
            var relationshipTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "integrates with", "integrates with" },
                { "sends", "sends data to" },
                { "receives", "receives data from" },
                { "depends on", "depends on" },
                { "uses", "uses" },
                { "is part of", "is part of" },
                { "contains", "contains" },
                { "manages", "manages" },
                { "reports to", "reports to" },
                { "communicates with", "communicates with" },
                { "interacts with", "interacts with" },
                { "connects to", "connects to" },
                { "is related to", "is related to" }
            };

            foreach (var type in relationshipTypes)
            {
                if (response.Contains(type.Key, StringComparison.OrdinalIgnoreCase))
                {
                    return type.Value;
                }
            }

            // Default relationship type
            return "relates to";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting relationship type from response");
            return "relates to";
        }
    }

    private string ExtractDescription(string response)
    {
        try
        {
            // Clean up the response and use it as the description
            return response.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting description from response");
            return "";
        }
    }

    private float CalculateWeight(string response)
    {
        try
        {
            // Look for strength indicators in the response
            var strengthIndicators = new Dictionary<string, float>
            {
                { "strong", 0.8f },
                { "significant", 0.7f },
                { "important", 0.7f },
                { "direct", 0.8f },
                { "clear", 0.7f },
                { "obvious", 0.7f },
                { "evident", 0.6f },
                { "weak", 0.3f },
                { "minor", 0.3f },
                { "potential", 0.4f },
                { "possible", 0.4f },
                { "indirect", 0.4f },
                { "unclear", 0.3f }
            };

            float weight = 0.5f; // Default weight

            foreach (var indicator in strengthIndicators)
            {
                if (response.Contains(indicator.Key, StringComparison.OrdinalIgnoreCase))
                {
                    weight = Math.Max(weight, indicator.Value);
                }
            }

            // Look for explicit numeric strength indicators (e.g., "strength: 0.8")
            var strengthMatch = System.Text.RegularExpressions.Regex.Match(response, @"strength:?\s*(\d+(\.\d+)?)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (strengthMatch.Success && float.TryParse(strengthMatch.Groups[1].Value, out float explicitStrength))
            {
                // Normalize to 0-1 range if necessary
                if (explicitStrength > 1)
                {
                    explicitStrength = Math.Min(explicitStrength / 10, 1.0f);
                }
                weight = explicitStrength;
            }

            return weight;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating weight from response");
            return 0.5f; // Default weight
        }
    }

    private List<string> ExtractKeywords(string response)
    {
        try
        {
            var keywords = new List<string>();

            // Look for explicit keywords section
            var keywordsMatch = System.Text.RegularExpressions.Regex.Match(response, @"keywords:?\s*([^\n.]+)[\n.]?", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (keywordsMatch.Success)
            {
                var keywordsText = keywordsMatch.Groups[1].Value.Trim();
                keywords.AddRange(keywordsText.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(k => k.Trim())
                    .Where(k => !string.IsNullOrWhiteSpace(k)));
            }

            // If no explicit keywords found, extract important terms
            if (keywords.Count == 0)
            {
                // Extract nouns and noun phrases as keywords
                var words = response.Split(new[] { ' ', '\t', '\n', '.', ',', ';', ':', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
                var importantWords = words.Where(w => w.Length > 4 && char.IsUpper(w[0])).Select(w => w.Trim()).Distinct();
                keywords.AddRange(importantWords);

                // Limit to top 5 keywords
                keywords = keywords.Take(5).ToList();
            }

            return keywords;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting keywords from response");
            return new List<string>();
        }
    }
}