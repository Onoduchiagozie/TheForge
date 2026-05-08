 
using TheForge.Models;

namespace TheForge.Servcies;

/// <summary>
/// Contract for the proxy client that talks to the Python FastAPI service
/// running at http://localhost:8000.
/// </summary>
public interface IFastApiClient
{
    // ── Query family ──────────────────────────────────────────────────────────

    /// <summary>POST /query — grounded, synchronous answer.</summary>
    Task<QueryResponse> QueryAsync(QueryRequest request, CancellationToken ct = default);

    /// <summary>POST /query/narrate — narrative reconstruction mode.</summary>
    Task<QueryResponse> QueryNarrateAsync(QueryRequest request, CancellationToken ct = default);

    /// <summary>POST /query/stream — SSE token stream (buffered into full string here).</summary>
    IAsyncEnumerable<string> QueryStreamAsync(QueryRequest request, CancellationToken ct = default);

    /// <summary>POST /query/narrate/stream — streaming narrator mode.</summary>
    IAsyncEnumerable<string> QueryNarrateStreamAsync(QueryRequest request, CancellationToken ct = default);

    /// <summary>POST /query/explore — explorer / object-inspection mode.</summary>
    Task<QueryResponse> QueryExplorerAsync(QueryRequest request, CancellationToken ct = default);

    // ── System ────────────────────────────────────────────────────────────────

    Task<HealthPayload> HealthAsync(CancellationToken ct = default);
    Task<InfoPayload> InfoAsync(CancellationToken ct = default);

    // ── Sources ───────────────────────────────────────────────────────────────

    Task<SourceListPayload> ListSourcesAsync(CancellationToken ct = default);
    Task<SourceChunksPayload> GetSourceChunksAsync(string sourceName, int limit = 20, CancellationToken ct = default);

    // ── Memory / Session ──────────────────────────────────────────────────────

    Task<MemoryPayload> GetMemoryAsync(string sessionId, CancellationToken ct = default);
    Task<Empty> ClearMemoryAsync(string sessionId, CancellationToken ct = default);
}