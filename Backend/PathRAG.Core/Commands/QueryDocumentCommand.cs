using MediatR;
using PathRAG.Core.Services;
using PathRAG.Core.Services.Query;
using PathRAG.Core.Services.Embedding;
using PathRAG.Core.Models;
using PathRAG.Core.Services.Cache;

namespace PathRAG.Core.Commands;

public record QueryDocumentCommand(string Query) : IRequest<string>;

public class QueryDocumentCommandHandler : IRequestHandler<QueryDocumentCommand, string>
{
    private readonly IKeywordExtractionService _keywordExtractor;
    private readonly IHybridQueryService _hybridQueryService;
    private readonly IContextBuilderService _contextBuilder;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILLMService _llmService;
    private readonly ILLMResponseCacheService _cacheService;

    public QueryDocumentCommandHandler(
        IKeywordExtractionService keywordExtractor,
        IHybridQueryService hybridQueryService,
        IContextBuilderService contextBuilder,
        IEmbeddingService embeddingService,
        ILLMService llmService,
        ILLMResponseCacheService cacheService)
    {
        _keywordExtractor = keywordExtractor;
        _hybridQueryService = hybridQueryService;
        _contextBuilder = contextBuilder;
        _embeddingService = embeddingService;
        _llmService = llmService;
        _cacheService = cacheService;
    }

    public async Task<string> Handle(QueryDocumentCommand request, CancellationToken cancellationToken)
    {
        // 1. Check cache first
        var cachedResponse = await _cacheService.GetResponseAsync(request.Query, cancellationToken);
        if (cachedResponse != null)
        {
            return cachedResponse;
        }

        // 2. Extract keywords from query
        var (highLevelKeywords, lowLevelKeywords) = await _keywordExtractor.ExtractKeywordsAsync(
            request.Query, 
            cancellationToken
        );

        // 3. Get query embedding
        var queryEmbedding = await _embeddingService.GetEmbeddingAsync(request.Query, cancellationToken);

        // 4. Perform hybrid search
        var searchResults = await _hybridQueryService.SearchAsync(
            queryEmbedding,
            highLevelKeywords,
            lowLevelKeywords,
            cancellationToken
        );

        // 5. Build context from search results
        var context = await _contextBuilder.BuildContextAsync(
            searchResults.Chunks,
            searchResults.Entities,
            searchResults.Relationships,
            cancellationToken
        );

        // 6. Generate response using LLM
        var prompt = $"Based on the following context, answer the question: {request.Query}\n\nContext:\n{context}";
        var response = await _llmService.GenerateResponseAsync(prompt, cancellationToken);

        // 7. Cache the response
        await _cacheService.CacheResponseAsync(request.Query, response, cancellationToken);

        return response;
    }
}
