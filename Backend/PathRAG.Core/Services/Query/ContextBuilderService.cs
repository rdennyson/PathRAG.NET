using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PathRAG.Core.Models;
using PathRAG.Infrastructure.Models;
using SharpToken;
using System.Text;

namespace PathRAG.Core.Services.Query;

public class ContextBuilderService : IContextBuilderService
{
    private readonly ILogger<ContextBuilderService> _logger;
    private readonly PathRagOptions _options;
    private readonly GptEncoding _encoding;

    // Maximum tokens for context (80% of model's context limit to leave room for prompt and completion)
    private readonly int _maxContextTokens;

    public ContextBuilderService(
        IOptions<PathRagOptions> options,
        ILogger<ContextBuilderService> logger)
    {
        _options = options.Value;
        _logger = logger;

        // Initialize the tokenizer based on the model
        _encoding = GptEncoding.GetEncodingForModel(GetTikTokenModelName(_options.CompletionModel));

        // Set maximum context tokens (80% of model's context limit)
        _maxContextTokens = 100000; // Approximately 80% of the model's context limit
    }

    private string GetTikTokenModelName(string modelName)
    {
        // Map Azure OpenAI model names to tiktoken model names
        return modelName.ToLower() switch
        {
            var name when name.Contains("gpt-4") => "gpt-4",
            var name when name.Contains("gpt-35") => "gpt-3.5-turbo",
            _ => "gpt-3.5-turbo" // Default to gpt-3.5-turbo encoding
        };
    }

    public async Task<string> BuildContextAsync(
        IReadOnlyList<TextChunk> chunks,
        IReadOnlyList<GraphEntity> entities,
        IReadOnlyList<Relationship> relationships,
        CancellationToken cancellationToken = default)
    {
        var context = new StringBuilder();
        int currentTokenCount = 0;
        int maxTokensPerSection = _maxContextTokens / 3; // Divide available tokens between chunks, entities, and relationships

        try
        {
            // Add text chunks (prioritize most relevant chunks)
            if (chunks.Any())
            {
                context.AppendLine("Relevant Text Passages:");
                var sortedChunks = chunks.OrderByDescending(c => c.TokenCount).ToList(); // Prioritize by token count as a proxy for relevance

                foreach (var chunk in sortedChunks)
                {
                    string chunkText = $"- {chunk.Content}\n";
                    int chunkTokens = _encoding.Encode(chunkText).Count;

                    if (currentTokenCount + chunkTokens > maxTokensPerSection)
                    {
                        _logger.LogInformation("Reached token limit for text chunks section: {TokenCount}/{MaxTokens}",
                            currentTokenCount, maxTokensPerSection);
                        break;
                    }

                    context.AppendLine(chunkText);
                    currentTokenCount += chunkTokens;
                }
                context.AppendLine();
            }

            // Reset token count for entities section
            currentTokenCount = 0;

            // Add entities
            if (entities.Any())
            {
                context.AppendLine("Related Entities:");
                var sortedEntities = entities.OrderByDescending(e => e.Weight).ToList(); // Prioritize by weight/importance

                foreach (var entity in sortedEntities)
                {
                    string entityText = $"- {entity.Name} ({entity.Type}): {entity.Description}\n";
                    int entityTokens = _encoding.Encode(entityText).Count;

                    if (currentTokenCount + entityTokens > maxTokensPerSection)
                    {
                        _logger.LogInformation("Reached token limit for entities section: {TokenCount}/{MaxTokens}",
                            currentTokenCount, maxTokensPerSection);
                        break;
                    }

                    context.AppendLine(entityText);
                    currentTokenCount += entityTokens;
                }
                context.AppendLine();
            }

            // Reset token count for relationships section
            currentTokenCount = 0;

            // Add relationships
            if (relationships.Any())
            {
                // Create a dictionary to look up entity names by ID
                var entityNameMap = entities.ToDictionary(
                    e => e.Id.ToString(),
                    e => e.Name);

                context.AppendLine("Entity Relationships:");
                var sortedRelationships = relationships.OrderByDescending(r => r.Weight).ToList(); // Prioritize by weight/importance

                foreach (var rel in sortedRelationships)
                {
                    // Try to get source and target entity names from the map
                    string sourceEntityName = GetEntityName(entityNameMap, rel.SourceEntityId);
                    string targetEntityName = GetEntityName(entityNameMap, rel.TargetEntityId);

                    string relText = $"- {sourceEntityName} {rel.Type} {targetEntityName}: {rel.Description}\n";
                    int relTokens = _encoding.Encode(relText).Count;

                    if (currentTokenCount + relTokens > maxTokensPerSection)
                    {
                        _logger.LogInformation("Reached token limit for relationships section: {TokenCount}/{MaxTokens}",
                            currentTokenCount, maxTokensPerSection);
                        break;
                    }

                    context.AppendLine(relText);
                    currentTokenCount += relTokens;
                }
            }

            string finalContext = context.ToString().Trim();
            int totalTokens = _encoding.Encode(finalContext).Count;
            _logger.LogInformation("Built context with {TokenCount} tokens", totalTokens);

            return finalContext;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building context");
            return context.ToString().Trim();
        }
    }

    private string GetEntityName(Dictionary<string, string> entityNameMap, string entityId)
    {
        // Try to get the entity name from the map, or use the ID if not found
        return entityNameMap.TryGetValue(entityId, out var name) ? name : entityId;
    }
}