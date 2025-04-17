using Azure.AI.OpenAI;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PathRAG.Core.Models;
using PathRAG.Core.Services.Cache;
using PathRAG.Core.Services.Embedding;
using PathRAG.Core.Services.Query;
using PathRAG.Infrastructure.Data;
using PathRAG.Infrastructure.Models;
using SharpToken;
using System.Text;

namespace PathRAG.Core.Queries;

public enum SearchMode
{
    Semantic,
    Hybrid,
    Graph
}

public class RAGQuery : IRequest<QueryResult>
{
    public string Query { get; set; } = string.Empty;
    public List<Guid> VectorStoreIds { get; set; } = new List<Guid>();
    public SearchMode SearchMode { get; set; } = SearchMode.Hybrid;
    public Guid AssistantId { get; set; }
    public string UserId { get; set; } = string.Empty;
}

public class QueryResult
{
    public string Answer { get; set; } = string.Empty;
    public List<string> Sources { get; set; } = new List<string>();
    public List<GraphEntity>? Entities { get; set; }
    public List<Relationship>? Relationships { get; set; }
    public List<string>? HighLevelKeywords { get; set; }
    public List<string>? LowLevelKeywords { get; set; }
    public bool IsCached { get; set; } = false;
}

public class QueryHandler : IRequestHandler<RAGQuery, QueryResult>
{
    private readonly PathRagDbContext _dbContext;
    private readonly IEmbeddingService _embeddingService;
    private readonly IHybridQueryService _hybridQueryService;
    private readonly IContextBuilderService _contextBuilderService;
    private readonly IKeywordExtractionService _keywordExtractionService;
    private readonly ILLMResponseCacheService _cacheService;
    private readonly OpenAIClient _openAIClient;
    private readonly PathRagOptions _options;
    private readonly ILogger<QueryHandler> _logger;
    private readonly GptEncoding _encoding;

    public QueryHandler(
        PathRagDbContext dbContext,
        IEmbeddingService embeddingService,
        IHybridQueryService hybridQueryService,
        IContextBuilderService contextBuilderService,
        IKeywordExtractionService keywordExtractionService,
        ILLMResponseCacheService cacheService,
        OpenAIClient openAIClient,
        IOptions<PathRagOptions> options,
        ILogger<QueryHandler> logger)
    {
        _dbContext = dbContext;
        _embeddingService = embeddingService;
        _hybridQueryService = hybridQueryService;
        _contextBuilderService = contextBuilderService;
        _keywordExtractionService = keywordExtractionService;
        _cacheService = cacheService;
        _openAIClient = openAIClient;
        _options = options.Value;
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

    public async Task<QueryResult> Handle(RAGQuery request, CancellationToken cancellationToken)
    {
        // 1. Check cache first
        var cachedResponse = await _cacheService.GetResponseAsync(request.Query, cancellationToken);
        if (cachedResponse != null)
        {
            _logger.LogInformation("Cache hit for query: {Query}", request.Query);
            return new QueryResult
            {
                Answer = cachedResponse,
                Sources = new List<string>(),
                IsCached = true
            };
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

        string answer = "I'm sorry, but I couldn't generate a response due to an error.";

        try
        {
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

            // Count tokens for each message
            int systemTokens = _encoding.Encode(systemMessage).Count;
            int contextTokens = _encoding.Encode(contextMessage).Count;
            int userTokens = _encoding.Encode(userMessage).Count;
            int totalTokens = systemTokens + contextTokens + userTokens;

            _logger.LogInformation("Token counts - System: {SystemTokens}, Context: {ContextTokens}, User: {UserTokens}, Total: {TotalTokens}",
                systemTokens, contextTokens, userTokens, totalTokens);

            // Check if we're exceeding the model's context limit
            int maxContextTokens = 120000; // Set a safe limit below the model's maximum
            if (totalTokens > maxContextTokens)
            {
                _logger.LogWarning("Total tokens ({TotalTokens}) exceed maximum context limit ({MaxContextTokens}). Truncating context.",
                    totalTokens, maxContextTokens);

                // Calculate how much we need to reduce the context
                int excessTokens = totalTokens - maxContextTokens + 2000; // Add buffer

                // Truncate the context
                var contextTokensList = _encoding.Encode(contextMessage);
                var truncatedContextTokens = contextTokensList.GetRange(0, Math.Max(0, contextTokensList.Count - excessTokens));
                contextMessage = _encoding.Decode(truncatedContextTokens);

                // Recalculate token count
                contextTokens = truncatedContextTokens.Count;
                totalTokens = systemTokens + contextTokens + userTokens;

                _logger.LogInformation("After truncation - Context: {ContextTokens}, Total: {TotalTokens}",
                    contextTokens, totalTokens);
            }

            // Generate answer using LLM
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

            // Get completion
            var completionResponse = await _openAIClient.GetChatCompletionsAsync(chatCompletionsOptions, cancellationToken);
            answer = completionResponse.Value.Choices[0].Message.Content;

            _logger.LogInformation("Successfully generated response with {TokenCount} tokens",
                totalTokens + _encoding.Encode(answer).Count);

            // Cache the response
            await _cacheService.CacheResponseAsync(request.Query, answer, cancellationToken);
            _logger.LogInformation("Cached response for query: {Query}", request.Query);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating response: {Message}", ex.Message);
            // Don't throw, use the default error message
        }

        // Create result
        var result = new QueryResult
        {
            Answer = answer,
            Sources = relevantChunks.Select(c => c.Content).ToList(),
            Entities = relevantEntities.Count > 0 ? relevantEntities : null,
            Relationships = relevantRelationships.Count > 0 ? relevantRelationships : null,
            HighLevelKeywords = highLevelKeywords,
            LowLevelKeywords = lowLevelKeywords,
            IsCached = false
        };

        return result;
    }
}
