using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using TheForge.Models;
using TheForge.Services;

namespace TheForge.Tests;

public class QueryCacheIsolationTests
{
    [Test]
    public void IdenticalQuestionFromDifferentSessionsDoesNotShareCachedAnswer()
    {
        using var memory = new MemoryCache(new MemoryCacheOptions { SizeLimit = 100 });
        var cache = new QueryCacheService(memory, NullLogger<QueryCacheService>.Instance);
        var first = new QueryRequest { Query = "Why did Horus turn traitor?", SessionId = "forge-user-a" };
        var second = new QueryRequest { Query = "Why did Horus turn traitor?", SessionId = "forge-user-b" };

        cache.Set(first, new QueryResponse { Response = "Session A only" });

        Assert.That(cache.Get(first)?.Response, Is.EqualTo("Session A only"));
        Assert.That(cache.Get(second), Is.Null);
    }

    [Test]
    public void FiltersArePartOfTheCacheIdentity()
    {
        using var memory = new MemoryCache(new MemoryCacheOptions { SizeLimit = 100 });
        var cache = new QueryCacheService(memory, NullLogger<QueryCacheService>.Instance);
        var first = new QueryRequest {
            Query = "Horus",
            SessionId = "forge-user-a",
            BookFilter = "False Gods",
            SourceFilter = ["one.pdf"]
        };
        var otherBook = new QueryRequest {
            Query = "Horus",
            SessionId = "forge-user-a",
            BookFilter = "Horus Rising",
            SourceFilter = ["one.pdf"]
        };
        var otherSource = new QueryRequest {
            Query = "Horus",
            SessionId = "forge-user-a",
            BookFilter = "False Gods",
            SourceFilter = ["two.pdf"]
        };
        cache.Set(first, new QueryResponse { Response = "First" });

        Assert.That(cache.Get(otherBook), Is.Null);
        Assert.That(cache.Get(otherSource), Is.Null);
        Assert.That(cache.Get(first)?.Response, Is.EqualTo("First"));
    }
}
