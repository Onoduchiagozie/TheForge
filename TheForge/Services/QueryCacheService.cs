using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using TheForge.Models;

namespace TheForge.Services;

/// <summary>
/// Ten-minute, per-session cache for synchronous responses.
/// Streaming requests are not cached.
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
        // Include every request dimension that affects retrieval and output.
        var session = req.SessionId?.Trim() ?? string.Empty;
        var query = req.Query.Trim().ToLowerInvariant();
        var book = req.BookFilter?.Trim().ToLowerInvariant() ?? string.Empty;
        var sources = string.Join("\u001f", (req.SourceFilter ?? [])
            .Where(source => !string.IsNullOrWhiteSpace(source))
            .Select(source => source.Trim())
            .OrderBy(source => source, StringComparer.Ordinal));
        var raw = string.Join("\u001e", session, query, book, sources);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return $"qcache:{Convert.ToHexString(hash).ToLowerInvariant()}";
    }
}
