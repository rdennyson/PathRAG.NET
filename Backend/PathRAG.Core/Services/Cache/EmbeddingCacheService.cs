using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using PathRAG.Core;

namespace PathRAG.Core.Services.Cache;

public class EmbeddingCacheEntry
{
    public float[]? Embedding { get; set; }
    public string? OriginalText { get; set; }
    public DateTime Timestamp { get; set; }
}

public class EmbeddingCacheService : IEmbeddingCacheService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<EmbeddingCacheService> _logger;
    private readonly PathRagOptions _options;
    private readonly TimeSpan _cacheExpiration;

    public EmbeddingCacheService(
        IDistributedCache cache,
        ILogger<EmbeddingCacheService> logger,
        IOptions<PathRagOptions> options)
    {
        _cache = cache;
        _logger = logger;
        _options = options.Value;
        _cacheExpiration = TimeSpan.FromMinutes(_options.CacheExpirationMinutes);
    }

    public async Task<float[]?> GetEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheKey = ComputeCacheKey(text);
            var cachedData = await _cache.GetStringAsync(cacheKey, cancellationToken);

            if (string.IsNullOrEmpty(cachedData))
                return null;

            var cacheEntry = JsonSerializer.Deserialize<EmbeddingCacheEntry>(cachedData);
            return cacheEntry?.Embedding;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving embedding from cache");
            return null;
        }
    }

    public async Task<(float[]? embedding, string? originalText)> GetSimilarEmbeddingAsync(
        string text,
        float[] queryEmbedding,
        float similarityThreshold = 0.95f,
        CancellationToken cancellationToken = default)
    {
        if (!_options.EnableEmbeddingCache)
        {
            return (null, null);
        }

        try
        {
            // Get all cache keys with the same prefix
            var cacheKeys = await GetAllCacheKeysAsync(cancellationToken);

            if (cacheKeys.Count == 0)
            {
                return (null, null);
            }

            float bestSimilarity = 0;
            float[]? bestEmbedding = null;
            string? bestOriginalText = null;

            foreach (var key in cacheKeys)
            {
                var cachedData = await _cache.GetStringAsync(key, cancellationToken);
                if (string.IsNullOrEmpty(cachedData))
                    continue;

                var cacheEntry = JsonSerializer.Deserialize<EmbeddingCacheEntry>(cachedData);
                if (cacheEntry == null || cacheEntry.Embedding == null)
                    continue;

                // Calculate cosine similarity
                float similarity = CalculateCosineSimilarity(queryEmbedding, cacheEntry.Embedding);

                if (similarity > similarityThreshold && similarity > bestSimilarity)
                {
                    bestSimilarity = similarity;
                    bestEmbedding = cacheEntry.Embedding;
                    bestOriginalText = cacheEntry.OriginalText;
                }
            }

            return (bestEmbedding, bestOriginalText);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving similar embedding from cache");
            return (null, null);
        }
    }

    private float CalculateCosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length)
            return 0;

        float dotProduct = 0;
        float normA = 0;
        float normB = 0;

        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        if (normA == 0 || normB == 0)
            return 0;

        return dotProduct / ((float)Math.Sqrt(normA) * (float)Math.Sqrt(normB));
    }

    private async Task<List<string>> GetAllCacheKeysAsync(CancellationToken cancellationToken)
    {
        // This is a simplified implementation that works with the distributed cache
        // In a real implementation, you would need to use a database or other storage that supports querying keys
        var cacheKeys = new List<string>();

        // If using Redis, you could use the KEYS command to get all keys with a specific pattern
        // For now, we'll use a simple approach with a separate index key that stores all cache keys
        var indexKey = "embedding_cache_index";
        var indexData = await _cache.GetStringAsync(indexKey, cancellationToken);

        if (!string.IsNullOrEmpty(indexData))
        {
            cacheKeys = JsonSerializer.Deserialize<List<string>>(indexData) ?? new List<string>();
        }

        return cacheKeys;
    }

    private async Task AddCacheKeyToIndexAsync(string cacheKey, CancellationToken cancellationToken)
    {
        var indexKey = "embedding_cache_index";
        var indexData = await _cache.GetStringAsync(indexKey, cancellationToken);
        var cacheKeys = new List<string>();

        if (!string.IsNullOrEmpty(indexData))
        {
            cacheKeys = JsonSerializer.Deserialize<List<string>>(indexData) ?? new List<string>();
        }

        if (!cacheKeys.Contains(cacheKey))
        {
            cacheKeys.Add(cacheKey);

            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = _cacheExpiration
            };

            await _cache.SetStringAsync(
                indexKey,
                JsonSerializer.Serialize(cacheKeys),
                options,
                cancellationToken
            );
        }
    }

    public async Task<(bool found, float[]? embedding)> TryGetEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheKey = ComputeCacheKey(text);
            var cachedData = await _cache.GetStringAsync(cacheKey, cancellationToken);

            if (string.IsNullOrEmpty(cachedData))
                return (false, null);

            var cacheEntry = JsonSerializer.Deserialize<EmbeddingCacheEntry>(cachedData);
            return (cacheEntry?.Embedding != null, cacheEntry?.Embedding);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving embedding from cache");
            return (false, null);
        }
    }

    public async Task CacheEmbeddingAsync(
        string text,
        float[] embedding,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheKey = ComputeCacheKey(text);
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = _cacheExpiration
            };

            var cacheEntry = new EmbeddingCacheEntry
            {
                Embedding = embedding,
                OriginalText = text,
                Timestamp = DateTime.UtcNow
            };

            var serializedData = JsonSerializer.Serialize(cacheEntry);
            await _cache.SetStringAsync(cacheKey, serializedData, options, cancellationToken);

            // Add the cache key to the index
            await AddCacheKeyToIndexAsync(cacheKey, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error caching embedding");
        }
    }

    public async Task<bool> HasEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = ComputeCacheKey(text);
        var cachedData = await _cache.GetStringAsync(cacheKey, cancellationToken);
        return !string.IsNullOrEmpty(cachedData);
    }

    public async Task RemoveEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheKey = ComputeCacheKey(text);
            await _cache.RemoveAsync(cacheKey, cancellationToken);

            // Remove the key from the index
            await RemoveCacheKeyFromIndexAsync(cacheKey, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing embedding from cache");
        }
    }

    private async Task RemoveCacheKeyFromIndexAsync(string cacheKey, CancellationToken cancellationToken)
    {
        var indexKey = "embedding_cache_index";
        var indexData = await _cache.GetStringAsync(indexKey, cancellationToken);
        var cacheKeys = new List<string>();

        if (!string.IsNullOrEmpty(indexData))
        {
            cacheKeys = JsonSerializer.Deserialize<List<string>>(indexData) ?? new List<string>();

            if (cacheKeys.Contains(cacheKey))
            {
                cacheKeys.Remove(cacheKey);

                var options = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = _cacheExpiration
                };

                await _cache.SetStringAsync(
                    indexKey,
                    JsonSerializer.Serialize(cacheKeys),
                    options,
                    cancellationToken
                );
            }
        }
    }

    private string ComputeCacheKey(string text)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(text));
        return Convert.ToBase64String(hashBytes);
    }
}