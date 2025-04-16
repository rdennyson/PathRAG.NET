using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace PathRAG.Core.Services.Cache;

public class LLMResponseCacheEntry
{
    public string? Query { get; set; }
    public string? Response { get; set; }
    public DateTime Timestamp { get; set; }
}

public class LLMResponseCacheService : ILLMResponseCacheService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<LLMResponseCacheService> _logger;
    private readonly PathRagOptions _options;
    private readonly TimeSpan _cacheExpiration;

    public LLMResponseCacheService(
        IDistributedCache cache,
        ILogger<LLMResponseCacheService> logger,
        IOptions<PathRagOptions> options)
    {
        _cache = cache;
        _logger = logger;
        _options = options.Value;
        _cacheExpiration = TimeSpan.FromMinutes(_options.CacheExpirationMinutes);
    }

    public async Task<string?> GetResponseAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        if (!_options.EnableLLMResponseCache)
        {
            return null;
        }

        try
        {
            // First try exact match
            var cacheKey = ComputeCacheKey(query);
            var cachedResponse = await _cache.GetStringAsync(cacheKey, cancellationToken);

            if (!string.IsNullOrEmpty(cachedResponse))
            {
                var cacheEntry = JsonSerializer.Deserialize<LLMResponseCacheEntry>(cachedResponse);
                return cacheEntry?.Response;
            }

            // If similarity-based caching is enabled, try to find a similar query
            if (_options.EnableSimilarityCache)
            {
                return await GetSimilarResponseAsync(query, cancellationToken);
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving LLM response from cache");
            return null;
        }
    }

    public async Task<string?> GetSimilarResponseAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        if (!_options.EnableLLMResponseCache || !_options.EnableSimilarityCache)
        {
            return null;
        }

        try
        {
            // Get all cache keys
            var cacheKeys = await GetAllCacheKeysAsync(cancellationToken);

            if (cacheKeys.Count == 0)
            {
                return null;
            }

            // Find the most similar query
            string? bestResponse = null;
            float bestSimilarity = 0;

            foreach (var key in cacheKeys)
            {
                var cachedData = await _cache.GetStringAsync(key, cancellationToken);
                if (string.IsNullOrEmpty(cachedData))
                    continue;

                var cacheEntry = JsonSerializer.Deserialize<LLMResponseCacheEntry>(cachedData);
                if (cacheEntry?.Query == null || cacheEntry.Response == null)
                    continue;

                // Calculate similarity between queries
                float similarity = CalculateStringSimilarity(query, cacheEntry.Query);

                if (similarity > _options.SimilarityThreshold && similarity > bestSimilarity)
                {
                    bestSimilarity = similarity;
                    bestResponse = cacheEntry.Response;
                }
            }

            return bestResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving similar LLM response from cache");
            return null;
        }
    }

    private float CalculateStringSimilarity(string a, string b)
    {
        // Simple Jaccard similarity for strings
        // In a real implementation, you would use embeddings for better similarity calculation
        var setA = new HashSet<string>(a.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        var setB = new HashSet<string>(b.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries));

        if (setA.Count == 0 || setB.Count == 0)
            return 0;

        var intersection = new HashSet<string>(setA);
        intersection.IntersectWith(setB);

        var union = new HashSet<string>(setA);
        union.UnionWith(setB);

        return (float)intersection.Count / union.Count;
    }

    private async Task<List<string>> GetAllCacheKeysAsync(CancellationToken cancellationToken)
    {
        var indexKey = "llm_response_cache_index";
        var indexData = await _cache.GetStringAsync(indexKey, cancellationToken);
        var cacheKeys = new List<string>();

        if (!string.IsNullOrEmpty(indexData))
        {
            cacheKeys = JsonSerializer.Deserialize<List<string>>(indexData) ?? new List<string>();
        }

        return cacheKeys;
    }

    public async Task CacheResponseAsync(
        string query,
        string response,
        CancellationToken cancellationToken = default)
    {
        if (!_options.EnableLLMResponseCache)
        {
            return;
        }

        try
        {
            var cacheKey = ComputeCacheKey(query);
            var cacheEntry = new LLMResponseCacheEntry
            {
                Query = query,
                Response = response,
                Timestamp = DateTime.UtcNow
            };

            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = _cacheExpiration
            };

            await _cache.SetStringAsync(
                cacheKey,
                JsonSerializer.Serialize(cacheEntry),
                options,
                cancellationToken
            );

            // Add the cache key to the index
            await AddCacheKeyToIndexAsync(cacheKey, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error caching LLM response");
        }
    }

    private async Task AddCacheKeyToIndexAsync(string cacheKey, CancellationToken cancellationToken)
    {
        var indexKey = "llm_response_cache_index";
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

    private static string ComputeCacheKey(string query)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(query));
        return Convert.ToBase64String(hash);
    }


}