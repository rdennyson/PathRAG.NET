using Azure.AI.OpenAI;
using Microsoft.Extensions.Options;
using PathRAG.Core.Services.Cache;

namespace PathRAG.Core.Services.Embedding;

public class EmbeddingService : IEmbeddingService
{
    private readonly OpenAIClient _openAIClient;
    private readonly PathRagOptions _options;
    private readonly IEmbeddingCacheService _cacheService;
    private const int BatchSize = 20; // Azure OpenAI recommended batch size

    public EmbeddingService(
        OpenAIClient openAIClient,
        IOptions<PathRagOptions> options,
        IEmbeddingCacheService cacheService)
    {
        _openAIClient = openAIClient;
        _options = options.Value;
        _cacheService = cacheService;
    }

    public async Task<float[]> GetEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        // Check cache first
        if (_options.EnableEmbeddingCache)
        {
            var (found, cachedEmbedding) = await _cacheService.TryGetEmbeddingAsync(text, cancellationToken);
            if (found && cachedEmbedding != null)
            {
                return cachedEmbedding;
            }
        }

        var options = new EmbeddingsOptions
        {
            DeploymentName = _options.EmbeddingDeployment,
            Input = { text }
        };

        var response = await _openAIClient.GetEmbeddingsAsync(options, cancellationToken);
        var embedding = response.Value.Data[0].Embedding.ToArray();

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
