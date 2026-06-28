using TheForge.Models;

namespace TheForge.Services;

public interface IFastApiClient
{
    Task<QueryResponse> QueryAsync(QueryRequest request, CancellationToken ct = default);
    Task<QueryResponse> QueryNarrateAsync(QueryRequest request, CancellationToken ct = default);
    Task<QueryResponse> QueryExplorerAsync(QueryRequest request, CancellationToken ct = default);
    IAsyncEnumerable<string> QueryStreamAsync(QueryRequest request, CancellationToken ct = default);
    IAsyncEnumerable<string> QueryNarrateStreamAsync(QueryRequest request, CancellationToken ct = default);
    Task<HealthPayload> HealthAsync(CancellationToken ct = default);
    Task<InfoPayload> InfoAsync(CancellationToken ct = default);
    Task<SourceListPayload> ListSourcesAsync(CancellationToken ct = default);
    Task<SourceChunksPayload> GetSourceChunksAsync(string sourceName, int limit = 20, CancellationToken ct = default);
    // GOLD: Context/session memory is intentionally disabled in the pre-Gold build.
    // Task<MemoryPayload> GetMemoryAsync(string sessionId, CancellationToken ct = default);
    // Task<Empty> ClearMemoryAsync(string sessionId, CancellationToken ct = default);
}
