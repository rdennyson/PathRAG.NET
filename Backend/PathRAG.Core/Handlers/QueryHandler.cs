using Azure.AI.OpenAI;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PathRAG.Core.Commands;
using PathRAG.Core.Models;
using PathRAG.Core.Services.Embedding;
using PathRAG.Core.Services.Query;
using PathRAG.Infrastructure.Data;

namespace PathRAG.Core.Handlers;

public class QueryHandler : IRequestHandler<QueryCommand, QueryResult>
{
    private readonly PathRagDbContext _dbContext;
    private readonly IEmbeddingService _embeddingService;
    private readonly IHybridQueryService _hybridQueryService;
    private readonly IContextBuilderService _contextBuilderService;
    private readonly OpenAIClient _openAIClient;
    private readonly PathRagOptions _options;
    private readonly ILogger<QueryHandler> _logger;

    public QueryHandler(
        PathRagDbContext dbContext,
        IEmbeddingService embeddingService,
        IHybridQueryService hybridQueryService,
        IContextBuilderService contextBuilderService,
        OpenAIClient openAIClient,
        IOptions<PathRagOptions> options,
        ILogger<QueryHandler> logger)
    {
        _dbContext = dbContext;
        _embeddingService = embeddingService;
        _hybridQueryService = hybridQueryService;
        _contextBuilderService = contextBuilderService;
        _openAIClient = openAIClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<QueryResult> Handle(QueryCommand request, CancellationToken cancellationToken)
    {
        // Verify assistant exists and belongs to user
        var assistant = await _dbContext.Assistants
            .Include(a => a.AssistantVectorStores)
            .FirstOrDefaultAsync(a => a.Id == request.AssistantId && a.UserId == request.UserId, cancellationToken);

        if (assistant == null)
        {
            throw new KeyNotFoundException($"Assistant with ID {request.AssistantId} not found");
        }

        // Get vector store IDs to search
        var vectorStoreIds = request.VectorStoreIds.Count > 0 
            ? request.VectorStoreIds 
            : assistant.AssistantVectorStores.Select(avs => avs.VectorStoreId).ToList();

        if (vectorStoreIds.Count == 0)
        {
            throw new InvalidOperationException("No vector stores specified for search");
        }

        // Get query embedding
        var queryEmbedding = await _embeddingService.GetEmbeddingAsync(request.Query, cancellationToken);

        // Perform search based on mode
        List<TextChunk> relevantChunks = new();
        List<GraphEntity> relevantEntities = new();
        List<Relationship> relevantRelationships = new();

        switch (request.SearchMode)
        {
            case SearchMode.Semantic:
                // Semantic search - vector similarity only
                relevantChunks = await _hybridQueryService.SemanticSearchAsync(
                    queryEmbedding,
                    vectorStoreIds,
                    _options.TopK,
                    cancellationToken);
                break;

            case SearchMode.Hybrid:
                // Hybrid search - vector + keyword
                relevantChunks = await _hybridQueryService.HybridSearchAsync(
                    request.Query,
                    queryEmbedding,
                    vectorStoreIds,
                    _options.TopK,
                    cancellationToken);
                break;

            case SearchMode.Graph:
                // Graph search - vector + keyword + graph
                (relevantChunks, relevantEntities, relevantRelationships) = await _hybridQueryService.GraphSearchAsync(
                    request.Query,
                    queryEmbedding,
                    vectorStoreIds,
                    _options.TopK,
                    cancellationToken);
                break;
        }

        // Build context from search results
        var context = await _contextBuilderService.BuildContextAsync(
            relevantChunks,
            relevantEntities,
            relevantRelationships,
            cancellationToken);

        // Generate answer using LLM
        var chatCompletionsOptions = new ChatCompletionsOptions
        {
            DeploymentName = _options.CompletionModel,
            Temperature = assistant.Temperature,
            MaxTokens = _options.MaxTokens
        };

        // Add system message
        chatCompletionsOptions.Messages.Add(new ChatRequestSystemMessage(assistant.Message));
        
        // Add context message
        chatCompletionsOptions.Messages.Add(new ChatRequestSystemMessage($"Context information:\n{context}"));
        
        // Add user query
        chatCompletionsOptions.Messages.Add(new ChatRequestUserMessage(request.Query));

        // Get completion
        var completionResponse = await _openAIClient.GetChatCompletionsAsync(chatCompletionsOptions, cancellationToken);
        var answer = completionResponse.Value.Choices[0].Message.Content;

        // Create result
        var result = new QueryResult
        {
            Answer = answer,
            Sources = relevantChunks.Select(c => c.Content).ToList(),
            Entities = relevantEntities.Count > 0 ? relevantEntities : null,
            Relationships = relevantRelationships.Count > 0 ? relevantRelationships : null
        };

        return result;
    }
}
