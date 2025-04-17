using Azure.AI.OpenAI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PathRAG.Core.Models;
using PathRAG.Core.Services.Embedding;
using PathRAG.Infrastructure.Models;
using SharpToken;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PathRAG.Core.Services.Entity;

public class EntityExtractionService : IEntityExtractionService
{
    private readonly OpenAIClient _openAIClient;
    private readonly PathRagOptions _options;
    private readonly IEntityEmbeddingService _embeddingService;
    private readonly ILogger<EntityExtractionService> _logger;
    private readonly GptEncoding _encoding;

    public EntityExtractionService(
        OpenAIClient openAIClient,
        IOptions<PathRagOptions> options,
        IEntityEmbeddingService embeddingService,
        ILogger<EntityExtractionService> logger)
    {
        _openAIClient = openAIClient;
        _options = options.Value;
        _embeddingService = embeddingService;
        _logger = logger;

        // Initialize the tokenizer based on the model
        _encoding = GptEncoding.GetEncodingForModel(GetTikTokenModelName(_options.CompletionModel));
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

    public async Task<ExtractionResult> ExtractEntitiesAndRelationshipsAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        var result = new ExtractionResult();

        try
        {
            // Check if the text is too large for the model
            var tokens = _encoding.Encode(text);
            _logger.LogInformation("Document has {TokenCount} tokens", tokens.Count);

            if (tokens.Count > _options.MaxInputTokens)
            {
                _logger.LogWarning("Document exceeds maximum token limit ({TokenCount} > {MaxTokens}). Chunking document for processing.",
                    tokens.Count, _options.MaxInputTokens);

                // Process the document in chunks
                return await ProcessLargeDocument(text, cancellationToken);
            }

            // First pass: Extract initial entities and relationships
            var initialExtraction = await ExtractInitialEntitiesAndRelationships(text, cancellationToken);
            MergeExtractionResults(result, initialExtraction);

            // Multiple passes for gleaning additional information
            var history = new List<ChatRequestMessage>();
            for (int i = 0; i < _options.EntityExtractMaxGleaning; i++)
            {
                var gleaningResult = await PerformGleaningPass(text, history, cancellationToken);
                if (!gleaningResult.ShouldContinue)
                    break;

                history.AddRange(gleaningResult.Messages);
                MergeExtractionResults(result, gleaningResult.Extraction);
            }

            // Summarize entity descriptions if they exceed token limit
            await SummarizeEntityDescriptions(result, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting entities and relationships");
        }

        return result;
    }

    private async Task<ExtractionResult> ProcessLargeDocument(
        string text,
        CancellationToken cancellationToken)
    {
        var result = new ExtractionResult();

        try
        {
            // Tokenize the document
            var tokens = _encoding.Encode(text);

            // Calculate chunk size (leaving room for system prompt and completion)
            int maxChunkTokens = 100000; // Approximately 80% of the model's context limit

            // Create chunks
            var chunks = new List<string>();
            for (int i = 0; i < tokens.Count; i += maxChunkTokens)
            {
                int chunkSize = Math.Min(maxChunkTokens, tokens.Count - i);
                var chunkTokens = tokens.GetRange(i, chunkSize);
                var chunkText = _encoding.Decode(chunkTokens);
                chunks.Add(chunkText);
            }

            _logger.LogInformation("Split document into {ChunkCount} chunks for processing", chunks.Count);

            // Process each chunk
            foreach (var chunk in chunks)
            {
                _logger.LogInformation("Processing chunk with {TokenCount} tokens", _encoding.Encode(chunk).Count);

                // Extract entities and relationships from this chunk
                var chunkExtraction = await ExtractInitialEntitiesAndRelationships(chunk, cancellationToken);

                // Merge results
                MergeExtractionResults(result, chunkExtraction);
            }

            // Summarize entity descriptions if they exceed token limit
            await SummarizeEntityDescriptions(result, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing large document");
        }

        return result;
    }

    private async Task<ExtractionResult> ExtractInitialEntitiesAndRelationships(
        string text,
        CancellationToken cancellationToken)
    {
        var prompt = new ChatCompletionsOptions
        {
            DeploymentName = _options.CompletionModel,
            Messages =
            {
                new ChatRequestSystemMessage(@"Extract entities and their relationships from the given text. Return your response as a JSON object with the following structure:

```json
{
  ""entities"": [
    {
      ""name"": ""Entity Name"",
      ""type"": ""Entity Type"",
      ""description"": ""Entity Description"",
      ""keywords"": [""keyword1"", ""keyword2""]
    },
    ...
  ],
  ""relationships"": [
    {
      ""source"": ""Source Entity Name"",
      ""target"": ""Target Entity Name"",
      ""type"": ""Relationship Type"",
      ""description"": ""Relationship Description"",
      ""keywords"": [""keyword1"", ""keyword2""]
    },
    ...
  ]
}
```

Entity types should be one of: organization, person, system, technology, location, concept, process.
Relationship types should be descriptive of the connection, such as: integrates with, sends data to, depends on, uses, is part of, contains, manages, reports to, communicates with.

Ensure your response is a valid JSON object and nothing else."),
                new ChatRequestUserMessage(text)
            },
            Temperature = _options.Temperature,
            MaxTokens = _options.MaxTokens,
            ResponseFormat = ChatCompletionsResponseFormat.JsonObject
        };

        var response = await _openAIClient.GetChatCompletionsAsync(
            prompt,
            cancellationToken
        );

        return ParseJsonResponse(response.Value.Choices[0].Message.Content);
    }

    private ExtractionResult ParseJsonResponse(string jsonResponse)
    {
        try
        {
            var result = new ExtractionResult();
            var extractionData = JsonSerializer.Deserialize<ExtractionData>(jsonResponse);

            if (extractionData == null)
            {
                _logger.LogError("Failed to deserialize JSON response: {Response}", jsonResponse);
                return result;
            }

            // Process entities
            var entityNameToId = new Dictionary<string, string>();

            foreach (var entityData in extractionData.Entities)
            {
                var entity = new GraphEntity
                {
                    Id = Guid.NewGuid(),
                    Name = SanitizeText(entityData.Name),
                    Type = SanitizeText(entityData.Type),
                    Description = SanitizeText(entityData.Description),
                    Keywords = entityData.Keywords?.Select(SanitizeText).ToList() ?? new List<string>(),
                    Weight = 1.0f
                };

                result.Entities.Add(entity);
                entityNameToId[entityData.Name] = entity.Id.ToString();
            }

            // Process relationships
            foreach (var relationshipData in extractionData.Relationships)
            {
                // Find the entity IDs
                if (entityNameToId.TryGetValue(relationshipData.Source, out string sourceId) &&
                    entityNameToId.TryGetValue(relationshipData.Target, out string targetId))
                {
                    var relationship = new Relationship
                    {
                        Id = Guid.NewGuid(),
                        SourceEntityId = sourceId,
                        TargetEntityId = targetId,
                        Type = SanitizeText(relationshipData.Type),
                        Description = SanitizeText(relationshipData.Description),
                        Keywords = relationshipData.Keywords?.Select(SanitizeText).ToList() ?? new List<string>(),
                        Weight = 1.0f
                    };

                    result.Relationships.Add(relationship);
                }
                else
                {
                    _logger.LogWarning("Could not find entities for relationship: {Source} -> {Target}",
                        relationshipData.Source, relationshipData.Target);
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing JSON response: {Response}", jsonResponse);
            return new ExtractionResult();
        }
    }

    private async Task<(ExtractionResult Extraction, bool ShouldContinue, List<ChatRequestMessage> Messages)> PerformGleaningPass(
        string text,
        List<ChatRequestMessage> history,
        CancellationToken cancellationToken)
    {
        var gleaningPrompt = new ChatCompletionsOptions
        {
            DeploymentName = _options.CompletionModel,
            Messages =
            {
                new ChatRequestSystemMessage(@"Identify additional entities and relationships that might have been missed in the previous extraction. Return your response as a JSON object with the following structure:

```json
{
  ""entities"": [
    {
      ""name"": ""Entity Name"",
      ""type"": ""Entity Type"",
      ""description"": ""Entity Description"",
      ""keywords"": [""keyword1"", ""keyword2""]
    },
    ...
  ],
  ""relationships"": [
    {
      ""source"": ""Source Entity Name"",
      ""target"": ""Target Entity Name"",
      ""type"": ""Relationship Type"",
      ""description"": ""Relationship Description"",
      ""keywords"": [""keyword1"", ""keyword2""]
    },
    ...
  ]
}
```

Focus on entities and relationships that weren't identified previously.

Entity types should be one of: organization, person, system, technology, location, concept, process.
Relationship types should be descriptive of the connection, such as: integrates with, sends data to, depends on, uses, is part of, contains, manages, reports to, communicates with.

Ensure your response is a valid JSON object and nothing else."),
                new ChatRequestUserMessage(text)
            },
            Temperature = _options.Temperature,
            MaxTokens = _options.MaxTokens,
            ResponseFormat = ChatCompletionsResponseFormat.JsonObject
        };

        var gleaningResponse = await _openAIClient.GetChatCompletionsAsync(
            gleaningPrompt,
            cancellationToken
        );

        var newMessages = new List<ChatRequestMessage>
        {
            new ChatRequestAssistantMessage(gleaningResponse.Value.Choices[0].Message.Content)
        };

        // Check if we should continue gleaning
        var continuePrompt = new ChatCompletionsOptions
        {
            DeploymentName = _options.CompletionModel,
            Messages =
            {
                new ChatRequestUserMessage("Should we continue looking for more entities and relationships? Answer only 'yes' or 'no'.")
            }
        };
        continuePrompt.Messages.Concat(history.Concat(newMessages));
        var continueResponse = await _openAIClient.GetChatCompletionsAsync(
            continuePrompt,
            cancellationToken
        );

        var shouldContinue = continueResponse.Value.Choices[0].Message.Content.Trim().ToLower() == "yes";
        var extractionResult = ParseJsonResponse(gleaningResponse.Value.Choices[0].Message.Content);

        return (extractionResult, shouldContinue, newMessages);
    }

    private async Task SummarizeEntityDescriptions(ExtractionResult result, CancellationToken cancellationToken)
    {
        foreach (var entity in result.Entities)
        {
            if (await ExceedsTokenLimit(entity.Description))
            {
                entity.Description = await SummarizeText(entity.Description, cancellationToken);
            }
        }

        foreach (var relationship in result.Relationships)
        {
            if (await ExceedsTokenLimit(relationship.Description))
            {
                relationship.Description = await SummarizeText(relationship.Description, cancellationToken);
            }
        }
    }

    private async Task<bool> ExceedsTokenLimit(string text)
    {
        var tokenCount = await _embeddingService.GetTokenCount(text);
        return tokenCount > _options.EntitySummaryMaxTokens;
    }

    private async Task<string> SummarizeText(string text, CancellationToken cancellationToken)
    {
        var prompt = new ChatCompletionsOptions
        {
            DeploymentName = _options.CompletionModel,
            Messages =
            {
                new ChatRequestSystemMessage("Summarize the following text while preserving key information:"),
                new ChatRequestUserMessage(text)
            },
            Temperature = _options.Temperature,
            MaxTokens = _options.EntitySummaryMaxTokens
        };

        var response = await _openAIClient.GetChatCompletionsAsync(
            prompt,
            cancellationToken
        );

        return response.Value.Choices[0].Message.Content;
    }

    private void MergeExtractionResults(ExtractionResult target, ExtractionResult source)
    {
        // Merge entities
        foreach (var entity in source.Entities)
        {
            var existingEntity = target.Entities.FirstOrDefault(e => e.Id == entity.Id);
            if (existingEntity == null)
            {
                target.Entities.Add(entity);
            }
            else
            {
                // Merge properties
                existingEntity.Keywords = existingEntity.Keywords.Union(entity.Keywords).ToList();
                existingEntity.Weight = Math.Max(existingEntity.Weight, entity.Weight);
                existingEntity.Description = CombineDescriptions(existingEntity.Description, entity.Description);
            }
        }

        // Merge relationships
        foreach (var relationship in source.Relationships)
        {
            var existingRelationship = target.Relationships.FirstOrDefault(r =>
                r.SourceEntityId == relationship.SourceEntityId &&
                r.TargetEntityId == relationship.TargetEntityId);

            if (existingRelationship == null)
            {
                target.Relationships.Add(relationship);
            }
            else
            {
                existingRelationship.Keywords = existingRelationship.Keywords.Union(relationship.Keywords).ToList();
                existingRelationship.Weight = Math.Max(existingRelationship.Weight, relationship.Weight);
                existingRelationship.Description = CombineDescriptions(existingRelationship.Description, relationship.Description);
            }
        }
    }

    private string CombineDescriptions(string desc1, string desc2)
    {
        var descriptions = new HashSet<string>(
            new[] { desc1, desc2 }
                .SelectMany(d => d.Split(new[] { ".", "\n" }, StringSplitOptions.RemoveEmptyEntries))
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
        );
        return string.Join(". ", descriptions);
    }

    private string SanitizeText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        // Remove null bytes (0x00) which cause issues with PostgreSQL UTF-8 encoding
        text = text.Replace("\0", "");

        // Remove other control characters except for newlines and tabs
        text = Regex.Replace(text, @"[\x00-\x08\x0B\x0C\x0E-\x1F]", "");

        // Replace multiple whitespace characters with a single space
        text = Regex.Replace(text, @"\s+", " ");

        // Trim leading/trailing whitespace
        return text.Trim();
    }
}