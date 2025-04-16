using Azure.AI.OpenAI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PathRAG.Core;
using PathRAG.Core.Services.Cache;

namespace PathRAG.Core.Services.Embedding;

public class EmbeddingService : IEmbeddingService
{
    private readonly OpenAIClient _openAIClient;
    private readonly PathRagOptions _options;
    private readonly IEmbeddingCacheService _cacheService;
    private readonly ILogger<EmbeddingService> _logger;
    private const int BatchSize = 20; // Azure OpenAI recommended batch size

    public EmbeddingService(
        OpenAIClient openAIClient,
        IOptions<PathRagOptions> options,
        IEmbeddingCacheService cacheService,
        ILogger<EmbeddingService> logger)
    {
        _openAIClient = openAIClient;
        _options = options.Value;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<float[]> GetEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        // Check cache first
        if (_options.EnableEmbeddingCache)
        {
            // First try exact match
            var (found, cachedEmbedding) = await _cacheService.TryGetEmbeddingAsync(text, cancellationToken);
            if (found && cachedEmbedding != null)
            {
                _logger.LogInformation("Using exact match from cache for text: {TextStart}...", text.Substring(0, Math.Min(50, text.Length)));
                return cachedEmbedding;
            }

            // If similarity-based caching is enabled, try to find a similar embedding
            if (_options.EnableSimilarityCache)
            {
                // First we need to get the embedding for the query text
                var options = new EmbeddingsOptions
                {
                    DeploymentName = _options.EmbeddingDeployment,
                    Input = { text }
                };

                var response = await _openAIClient.GetEmbeddingsAsync(options, cancellationToken);
                var queryEmbedding = response.Value.Data[0].Embedding.ToArray();

                // Now try to find a similar embedding in the cache
                var (similarEmbedding, originalText) = await _cacheService.GetSimilarEmbeddingAsync(
                    text,
                    queryEmbedding,
                    _options.SimilarityThreshold,
                    cancellationToken);

                if (similarEmbedding != null)
                {
                    _logger.LogInformation("Using similar match from cache. Original: {OriginalTextStart}...",
                        originalText?.Substring(0, Math.Min(50, originalText.Length)) ?? "unknown");

                    // Cache this text with the similar embedding to speed up future lookups
                    await _cacheService.CacheEmbeddingAsync(text, similarEmbedding, cancellationToken);

                    return similarEmbedding;
                }

                // If we got here, we already have the embedding, so just cache and return it
                await _cacheService.CacheEmbeddingAsync(text, queryEmbedding, cancellationToken);
                return queryEmbedding;
            }
        }

        // If we get here, either caching is disabled or no cache hit occurred
        var embOptions = new EmbeddingsOptions
        {
            DeploymentName = _options.EmbeddingDeployment,
            Input = { text }
        };

        var embResponse = await _openAIClient.GetEmbeddingsAsync(embOptions, cancellationToken);
        var embedding = embResponse.Value.Data[0].Embedding.ToArray();

        // Cache the result
        if (_options.EnableEmbeddingCache)
        {
            await _cacheService.CacheEmbeddingAsync(text, embedding, cancellationToken);
        }

        return embedding;
    }

    public async Task<IReadOnlyList<float[]>> GetEmbeddingsAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default)
    {
        var embeddings = new List<float[]>();

        // Process in batches to optimize API calls
        foreach (var batch in texts.Chunk(BatchSize))
        {
            // Check cache for each text in batch
            var uncachedTexts = new List<string>();
            var batchResults = new Dictionary<int, float[]>();

            for (int i = 0; i < batch.Length; i++)
            {
                if (_options.EnableEmbeddingCache)
                {
                    var (found, cachedEmbedding) = await _cacheService.TryGetEmbeddingAsync(batch[i], cancellationToken);
                    if (found && cachedEmbedding != null)
                    {
                        batchResults[i] = cachedEmbedding;
                    }
                    else
                    {
                        uncachedTexts.Add(batch[i]);
                    }
                }
                else
                {
                    uncachedTexts.Add(batch[i]);
                }

            }

            if (uncachedTexts.Any())
            {
                var response = await _openAIClient.GetEmbeddingsAsync(new EmbeddingsOptions(_options.EmbeddingDeployment, uncachedTexts), cancellationToken);

                // Cache and store results
                var uncachedIndex = 0;
                for (int i = 0; i < batch.Length; i++)
                {
                    if (!batchResults.ContainsKey(i))
                    {
                        var embedding = response.Value.Data[uncachedIndex].Embedding.ToArray();
                        if (_options.EnableEmbeddingCache)
                        {
                            await _cacheService.CacheEmbeddingAsync(batch[i], embedding, cancellationToken);
                        }
                        batchResults[i] = embedding;
                        uncachedIndex++;
                    }
                }
            }

            // Add results in correct order
            embeddings.AddRange(batchResults.OrderBy(kvp => kvp.Key).Select(kvp => kvp.Value));
        }

        return embeddings;
    }
}
