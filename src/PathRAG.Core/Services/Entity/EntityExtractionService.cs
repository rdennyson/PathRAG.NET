using System.Linq;
using System.Text.Json;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PathRAG.Core.Models;
using PathRAG.Core.Services.Embedding;

namespace PathRAG.Core.Services.Entity;

public class EntityExtractionService : IEntityExtractionService
{
    private readonly OpenAIClient _openAIClient;
    private readonly PathRagOptions _options;
    private readonly IEntityEmbeddingService _embeddingService;
    private readonly ILogger<EntityExtractionService> _logger;

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
    }

    public async Task<ExtractionResult> ExtractEntitiesAndRelationshipsAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        var result = new ExtractionResult();
        
        // First pass: Extract initial entities and relationships
        var initialExtraction = await ExtractInitialEntitiesAndRelationships(text, cancellationToken);
        
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

        return result;
    }

    private async Task<ExtractionResult> ExtractInitialEntitiesAndRelationships(
        string text, 
        CancellationToken cancellationToken)
    {
        var prompt = new ChatCompletionsOptions
        {
            Messages = 
            {
                new ChatRequestSystemMessage("Extract entities and their relationships from the given text. Include entity types, descriptions, and relationship details."),
                new ChatRequestUserMessage(text)
            },
            Temperature = _options.Temperature,
            MaxTokens = _options.MaxTokens
        };

        var response = await _openAIClient.GetChatCompletionsAsync(
            prompt,
            cancellationToken
        );

        return ParseExtractionResponse(response.Value.Choices[0].Message.Content);
    }

    private async Task<(ExtractionResult Extraction, bool ShouldContinue, List<ChatRequestMessage> Messages)> PerformGleaningPass(
        string text,
        List<ChatRequestMessage> history,
        CancellationToken cancellationToken)
    {
        var gleaningPrompt = new ChatCompletionsOptions
        {
            Messages = 
            {
                new ChatRequestSystemMessage("Identify additional entities and relationships that might have been missed in the previous extraction."),
                new ChatRequestUserMessage(text)
            },
            Temperature = _options.Temperature,
            MaxTokens = _options.MaxTokens
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
        var extractionResult = ParseExtractionResponse(gleaningResponse.Value.Choices[0].Message.Content);

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

    private ExtractionResult ParseExtractionResponse(string response)
    {
        try
        {
            return JsonSerializer.Deserialize<ExtractionResult>(response) 
                   ?? new ExtractionResult();
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse extraction response");
            return new ExtractionResult();
        }
    }
}