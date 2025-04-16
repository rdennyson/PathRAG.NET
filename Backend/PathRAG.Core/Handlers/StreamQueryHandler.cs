using Azure.AI.OpenAI;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PathRAG.Core.Commands;
using PathRAG.Core.Models;
using PathRAG.Core.Services;
using PathRAG.Core.Services.Cache;
using PathRAG.Core.Services.Embedding;
using PathRAG.Core.Services.Query;
using PathRAG.Core.Services.Vector;
using PathRAG.Infrastructure.Data;
using System.Runtime.CompilerServices;
using System.Text;

namespace PathRAG.Core.Handlers;

public class StreamQueryHandler : IRequestHandler<StreamQueryCommand, IAsyncEnumerable<string>>
{
    private readonly PathRagDbContext _dbContext;
    private readonly IVectorSearchService _vectorSearchService;
    private readonly IEmbeddingService _embeddingService;
    private readonly IHybridQueryService _hybridQueryService;
    private readonly IContextBuilderService _contextBuilderService;
    private readonly OpenAIClient _openAIClient;
    private readonly PathRagOptions _options;
    private readonly ILogger<StreamQueryHandler> _logger;

    public StreamQueryHandler(
        PathRagDbContext dbContext,
        IVectorSearchService vectorSearchService,
        IEmbeddingService embeddingService,
        IHybridQueryService hybridQueryService,
        IContextBuilderService contextBuilderService,
        OpenAIClient openAIClient,
        IOptions<PathRagOptions> options,
        ILogger<StreamQueryHandler> logger)
    {
        _dbContext = dbContext;
        _vectorSearchService = vectorSearchService;
        _embeddingService = embeddingService;
        _hybridQueryService = hybridQueryService;
        _contextBuilderService = contextBuilderService;
        _openAIClient = openAIClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IAsyncEnumerable<string>> Handle(StreamQueryCommand request, CancellationToken cancellationToken)
    {
        // Find assistant
        var assistant = await _dbContext.Assistants
            .FirstOrDefaultAsync(a => a.Id == request.AssistantId && a.UserId == request.UserId, cancellationToken);

        if (assistant == null)
        {
            throw new KeyNotFoundException($"Assistant with ID {request.AssistantId} not found");
        }

        // Get query embedding
        var queryEmbedding = await _embeddingService.GetEmbeddingAsync(request.Query, cancellationToken);

        // Perform search based on mode
        List<TextChunk> relevantChunks;
        List<GraphEntity> entities = new();
        List<Relationship> relationships = new();

        switch (request.SearchMode)
        {
            case SearchMode.Semantic:
                relevantChunks = await _hybridQueryService.SemanticSearchAsync(
                    queryEmbedding,
                    request.VectorStoreIds,
                    _options.TopK,
                    cancellationToken);
                break;

            case SearchMode.Hybrid:
                relevantChunks = await _hybridQueryService.HybridSearchAsync(
                    request.Query,
                    queryEmbedding,
                    request.VectorStoreIds,
                    _options.TopK,
                    cancellationToken);
                break;

            case SearchMode.Graph:
                var graphResults = await _hybridQueryService.GraphSearchAsync(
                    request.Query,
                    queryEmbedding,
                    request.VectorStoreIds,
                    _options.TopK,
                    cancellationToken);

                relevantChunks = graphResults.chunks;
                entities = graphResults.entities;
                relationships = graphResults.relationships;
                break;

            default:
                relevantChunks = await _hybridQueryService.HybridSearchAsync(
                    request.Query,
                    queryEmbedding,
                    request.VectorStoreIds,
                    _options.TopK,
                    cancellationToken);
                break;
        }

        // Build context from relevant chunks
        var context = await _contextBuilderService.BuildContextAsync(relevantChunks, entities, relationships, cancellationToken);

        // Build system message
        var systemMessage = assistant.Message;

        // Build context message
        var contextMessage = $"Here is some context that may help you answer the user's question:\n\n{context}";

        // Build user message
        var userMessage = request.Query;

        // Create chat completions options
        var chatCompletionsOptions = new ChatCompletionsOptions
        {
            Temperature = assistant.Temperature,
            MaxTokens = _options.MaxTokens
        };

        // Add system message
        chatCompletionsOptions.Messages.Add(new ChatRequestSystemMessage(systemMessage));

        // Add context message
        chatCompletionsOptions.Messages.Add(new ChatRequestSystemMessage(contextMessage));

        // Add user query
        chatCompletionsOptions.Messages.Add(new ChatRequestUserMessage(userMessage));

        // Return streaming response
        return StreamCompletions(chatCompletionsOptions, cancellationToken);
    }

    private async IAsyncEnumerable<string> StreamCompletions(
        ChatCompletionsOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        options.DeploymentName = _options.CompletionModel;
        // Get streaming response
        var response = await _openAIClient.GetChatCompletionsStreamingAsync(
            options,
            cancellationToken);

        // Stream the response chunks
        await foreach (var choice in response.WithCancellation(cancellationToken))
        {
            if (choice.ContentUpdate != null)
            {
                yield return choice.ContentUpdate;
            }
        }
    }
}
