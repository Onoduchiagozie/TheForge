using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using TheForge.Models;

namespace TheForge.Services;

/// <summary>
/// Caches synchronous query responses in memory.
/// Cache key = SHA256(query + mode + top_k) — same query+settings returns instantly.
/// TTL = 10 minutes.
/// Stream endpoints are intentionally excluded — you cannot cache a live SSE stream.
///
/// Usage in QueryController:
///   var cached = cacheService.Get(request);
///   if (cached is not null) return Ok(cached);
///   var result = await fastApi.QueryAsync(...);
///   cacheService.Set(request, result);
///   return Ok(result);
/// </summary>
public class QueryCacheService(IMemoryCache cache, ILogger<QueryCacheService> logger)
{
    private static readonly TimeSpan _ttl = TimeSpan.FromMinutes(10);

    public QueryResponse? Get(QueryRequest request)
    {
        var key = BuildKey(request);
        if (cache.TryGetValue(key, out QueryResponse? cached))
        {
            logger.LogInformation("Cache HIT for key {Key}", key[..12]);
            return cached;
        }
        return null;
    }

    public void Set(QueryRequest request, QueryResponse response)
    {
        var key = BuildKey(request);
        cache.Set(key, response, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = _ttl,
            Size = 1,   // requires cache size limit set in Program.cs
        });
        logger.LogInformation("Cache SET for key {Key} (TTL {Ttl}m)", key[..12], _ttl.Minutes);
    }

    public void Invalidate(QueryRequest request)
    {
        cache.Remove(BuildKey(request));
    }

    private static string BuildKey(QueryRequest req)
    {
        var raw = $"{req.Query.Trim().ToLowerInvariant()}|{req.Mode ?? "remembrancer"}|{req.TopK ?? 6}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return $"qcache:{Convert.ToHexString(hash).ToLowerInvariant()}";
    }
}
