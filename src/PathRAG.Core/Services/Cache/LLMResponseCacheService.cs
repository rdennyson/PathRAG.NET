using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace PathRAG.Core.Services.Cache;

public class LLMResponseCacheService : ILLMResponseCacheService
{
    private readonly IDistributedCache _cache;
    private static readonly TimeSpan _cacheExpiration = TimeSpan.FromDays(7);

    public LLMResponseCacheService(IDistributedCache cache)
    {
        _cache = cache;
    }

    public async Task<string?> GetResponseAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = ComputeCacheKey(query);
        var cachedResponse = await _cache.GetStringAsync(cacheKey, cancellationToken);

        if (string.IsNullOrEmpty(cachedResponse))
        {
            return null;
        }

        var cacheEntry = JsonSerializer.Deserialize<CacheEntry>(cachedResponse);
        return cacheEntry?.Response;
    }

    public async Task CacheResponseAsync(
        string query,
        string response,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = ComputeCacheKey(query);
        var cacheEntry = new CacheEntry
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
    }

    private static string ComputeCacheKey(string query)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(query));
        return Convert.ToBase64String(hash);
    }

    private class CacheEntry
    {
        public string Query { get; set; } = "";
        public string Response { get; set; } = "";
        public DateTime Timestamp { get; set; }
    }
}