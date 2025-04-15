using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace PathRAG.Core.Services.Cache;

public class EmbeddingCacheService : IEmbeddingCacheService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<EmbeddingCacheService> _logger;
    private static readonly TimeSpan _cacheExpiration = TimeSpan.FromDays(30);

    public EmbeddingCacheService(
        IDistributedCache cache,
        ILogger<EmbeddingCacheService> logger)
    {
        _cache = cache;
        _logger = logger;
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

            return JsonSerializer.Deserialize<float[]>(cachedData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving embedding from cache");
            return null;
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

            var embedding = JsonSerializer.Deserialize<float[]>(cachedData);
            return (true, embedding);
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

            var serializedData = JsonSerializer.Serialize(embedding);
            await _cache.SetStringAsync(cacheKey, serializedData, options, cancellationToken);
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing embedding from cache");
        }
    }

    private string ComputeCacheKey(string text)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(text));
        return Convert.ToBase64String(hashBytes);
    }
}