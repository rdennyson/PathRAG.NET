using Azure.AI.OpenAI;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PathRAG.Core.Models;
using PathRAG.Core.Services.Cache;
using PathRAG.Core.Services.Embedding;
using PathRAG.Core.Services.Query;
using PathRAG.Core.Services.Vector;
using PathRAG.Infrastructure.Data;
using PathRAG.Infrastructure.Models;
using System.Runtime.CompilerServices;
using System.Text;

namespace PathRAG.Core.Queries;

public class RAGStreamQuery : IRequest<IAsyncEnumerable<string>>
{
    public string Query { get; set; } = string.Empty;
    public List<Guid> VectorStoreIds { get; set; } = new List<Guid>();
    public SearchMode SearchMode { get; set; } = SearchMode.Hybrid;
    public Guid AssistantId { get; set; }
    public string UserId { get; set; } = string.Empty;
}

public class StreamQueryHandler : IRequestHandler<RAGStreamQuery, IAsyncEnumerable<string>>
{
    private readonly PathRagDbContext _dbContext;
    private readonly IVectorSearchService _vectorSearchService;
    private readonly IEmbeddingService _embeddingService;
    private readonly IHybridQueryService _hybridQueryService;
    private readonly IContextBuilderService _contextBuilderService;
    private readonly IKeywordExtractionService _keywordExtractionService;
    private readonly ILLMResponseCacheService _cacheService;
    private readonly OpenAIClient _openAIClient;
    private readonly PathRagOptions _options;
    private readonly ILogger<StreamQueryHandler> _logger;

    public StreamQueryHandler(
        PathRagDbContext dbContext,
        IVectorSearchService vectorSearchService,
        IEmbeddingService embeddingService,
        IHybridQueryService hybridQueryService,
        IContextBuilderService contextBuilderService,
        IKeywordExtractionService keywordExtractionService,
        ILLMResponseCacheService cacheService,
        OpenAIClient openAIClient,
        IOptions<PathRagOptions> options,
        ILogger<StreamQueryHandler> logger)
    {
        _dbContext = dbContext;
        _vectorSearchService = vectorSearchService;
        _embeddingService = embeddingService;
        _hybridQueryService = hybridQueryService;
        _contextBuilderService = contextBuilderService;
        _keywordExtractionService = keywordExtractionService;
        _cacheService = cacheService;
        _openAIClient = openAIClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IAsyncEnumerable<string>> Handle(RAGStreamQuery request, CancellationToken cancellationToken)
    {
        // 1. Check cache first
        var cachedResponse = await _cacheService.GetResponseAsync(request.Query, cancellationToken);
        if (cachedResponse != null)
        {
            _logger.LogInformation("Cache hit for query: {Query}", request.Query);
            return new List<string> { cachedResponse }.ToAsyncEnumerable();
        }

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

        // 2. Extract keywords from query
        var keywordResult = await _keywordExtractionService.ExtractKeywordsAsync(request.Query, cancellationToken);
        var highLevelKeywords = keywordResult.HighLevelKeywords.ToList();
        var lowLevelKeywords = keywordResult.LowLevelKeywords.ToList();

        _logger.LogInformation("Extracted keywords - High-level: {HighLevel}, Low-level: {LowLevel}",
            string.Join(", ", highLevelKeywords),
            string.Join(", ", lowLevelKeywords));

        // 3. Get query embedding
        var queryEmbedding = await _embeddingService.GetEmbeddingAsync(request.Query, cancellationToken);

        // 4. Perform search based on mode
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
                // Use the extracted keywords for better search results
                relevantChunks = await _hybridQueryService.HybridSearchAsync(
                    request.Query,
                    queryEmbedding,
                    vectorStoreIds,
                    _options.TopK,
                    cancellationToken,
                    highLevelKeywords,
                    lowLevelKeywords);
                break;

            case SearchMode.Graph:
                // Graph search - vector + keyword + graph
                (relevantChunks, relevantEntities, relevantRelationships) = await _hybridQueryService.GraphSearchAsync(
                    request.Query,
                    queryEmbedding,
                    vectorStoreIds,
                    _options.TopK,
                    cancellationToken,
                    highLevelKeywords,
                    lowLevelKeywords);
                break;
        }

        // Build context from search results
        var context = await _contextBuilderService.BuildContextAsync(
            relevantChunks,
            relevantEntities,
            relevantRelationships,
            cancellationToken);

        // Create the messages
        var systemMessage = assistant.Message;
        var contextMessage = $"Context information:\n{context}";
        var userMessage = request.Query;

        // Create chat completions options
        var chatCompletionsOptions = new ChatCompletionsOptions
        {
            DeploymentName = _options.CompletionModel,
            Temperature = assistant.Temperature,
            MaxTokens = _options.MaxTokens
        };

        // Add system message
        chatCompletionsOptions.Messages.Add(new ChatRequestSystemMessage(systemMessage));

        // Add context message
        chatCompletionsOptions.Messages.Add(new ChatRequestSystemMessage(contextMessage));

        // Add user query
        chatCompletionsOptions.Messages.Add(new ChatRequestUserMessage(userMessage));

        // Return streaming response with caching
        return StreamCompletionsWithCaching(chatCompletionsOptions, request.Query, cancellationToken);
    }

    private async IAsyncEnumerable<string> StreamCompletionsWithCaching(
        ChatCompletionsOptions options,
        string query,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Get streaming response
        var response = await _openAIClient.GetChatCompletionsStreamingAsync(
            options,
            cancellationToken);

        // Collect the full response for caching
        var fullResponse = new StringBuilder();

        // Stream the response chunks
        await foreach (var choice in response.WithCancellation(cancellationToken))
        {
            if (choice.ContentUpdate != null)
            {
                // Append to the full response
                fullResponse.Append(choice.ContentUpdate);

                // Yield the chunk to the client
                yield return choice.ContentUpdate;
            }
        }

        // Cache the complete response
        string completeResponse = fullResponse.ToString();
        if (!string.IsNullOrEmpty(completeResponse))
        {
            await _cacheService.CacheResponseAsync(query, completeResponse, cancellationToken);
            _logger.LogInformation("Cached streaming response for query: {Query}", query);
        }
    }
}
