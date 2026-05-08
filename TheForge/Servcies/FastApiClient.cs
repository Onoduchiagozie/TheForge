using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using TheForge.Models;

namespace TheForge.Servcies;

/// <summary>
/// HTTP proxy client for the Python FastAPI service.
/// Registered via AddHttpClient&lt;IFastApiClient, FastApiClient&gt; in Program.cs.
/// BaseAddress is set there — all URLs here are relative (no leading slash needed
/// when BaseAddress has a trailing slash).
/// </summary>
public class FastApiClient(HttpClient http, ILogger<FastApiClient> logger) : IFastApiClient
{
    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<T> PostAsync<T>(string path, object body, CancellationToken ct)
    {
        try
        {
            var resp = await http.PostAsJsonAsync(path, body, _json, ct);
            resp.EnsureSuccessStatusCode();
            Console.WriteLine(".......................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................................",
                resp.Content);
            return await resp.Content.ReadFromJsonAsync<T>(_json, ct)
                   ?? throw new InvalidOperationException($"Null response from POST {path}");
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "HTTP error calling POST {Path}", path);
            throw;
        }
    }

    private async Task<T> GetAsync<T>(string path, CancellationToken ct)
    {
        try
        {
            return await http.GetFromJsonAsync<T>(path, _json, ct)
                   ?? throw new InvalidOperationException($"Null response from GET {path}");
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "HTTP error calling GET {Path}", path);
            throw;
        }
    }

    // ── Query ─────────────────────────────────────────────────────────────────

    public Task<QueryResponse> QueryAsync(QueryRequest request, CancellationToken ct = default)
        => PostAsync<QueryResponse>("query", request, ct);

    public Task<QueryResponse> QueryNarrateAsync(QueryRequest request, CancellationToken ct = default)
        => PostAsync<QueryResponse>("query/narrate", request, ct);

    public Task<QueryResponse> QueryExplorerAsync(QueryRequest request, CancellationToken ct = default)
        => PostAsync<QueryResponse>("query/explore", request, ct);

    // ── Streaming ─────────────────────────────────────────────────────────────
    // IMPORTANT: do NOT use `using` on the HttpResponseMessage in stream methods.
    // Disposing the response disposes the stream before it is fully read.
    // The HttpClient itself manages lifetime here.

    public async IAsyncEnumerable<string> QueryStreamAsync(
        QueryRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var token in StreamSse("query/stream", request, ct))
            yield return token;
    }

    public async IAsyncEnumerable<string> QueryNarrateStreamAsync(
        QueryRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var token in StreamSse("query/narrate/stream", request, ct))
            yield return token;
    }

    /// <summary>
    /// Shared SSE reader. Sends a POST, reads lines, strips "data: " prefix,
    /// stops at [DONE], skips __SOURCES__ frames (handled by caller if needed).
    /// </summary>
    private async IAsyncEnumerable<string> StreamSse(
        string path,
        object body,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body, options: _json)
        };

        // ResponseHeadersRead = start reading as soon as headers arrive, don't buffer body
        var resp = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();

        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream && !ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line is null) yield break;
            if (!line.StartsWith("data: ")) continue;

            var payload = line["data: ".Length..];
            if (payload == "[DONE]") yield break;
            if (payload.StartsWith("__SOURCES__:")) continue;
            if (payload.StartsWith("[ERROR]"))
            {
                logger.LogError("FastAPI stream error: {Payload}", payload);
                yield return payload;
                yield break;
            }

            yield return payload;
        }
    }

    // ── System ────────────────────────────────────────────────────────────────

    public Task<HealthPayload> HealthAsync(CancellationToken ct = default)
        => GetAsync<HealthPayload>("health", ct);

    public Task<InfoPayload> InfoAsync(CancellationToken ct = default)
        => GetAsync<InfoPayload>("info", ct);

    // ── Sources ───────────────────────────────────────────────────────────────

    public Task<SourceListPayload> ListSourcesAsync(CancellationToken ct = default)
        => GetAsync<SourceListPayload>("sources", ct);

    public Task<SourceChunksPayload> GetSourceChunksAsync(
        string sourceName, int limit = 20, CancellationToken ct = default)
        => GetAsync<SourceChunksPayload>(
            $"sources/{Uri.EscapeDataString(sourceName)}?limit={limit}", ct);

    // ── Memory ────────────────────────────────────────────────────────────────

    public Task<MemoryPayload> GetMemoryAsync(string sessionId, CancellationToken ct = default)
        => GetAsync<MemoryPayload>(
            $"memory?session_id={Uri.EscapeDataString(sessionId)}", ct);

    public async Task<Empty> ClearMemoryAsync(string sessionId, CancellationToken ct = default)
    {
        try
        {
            var resp = await http.DeleteAsync(
                $"memory?session_id={Uri.EscapeDataString(sessionId)}", ct);
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadFromJsonAsync<Empty>(_json, ct)
                   ?? new Empty { Status = "cleared" };
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "HTTP error calling DELETE memory for session {SessionId}", sessionId);
            throw;
        }
    }
}
// using System.Net.Http.Json;
// using System.Runtime.CompilerServices;
// using System.Text.Json;
// using TheForge.Models;
//
// namespace TheForge.Servcies;
//
// /// <summary>
// /// HTTP proxy client for the Python FastAPI service.
// /// Registered via AddHttpClient&lt;IFastApiClient, FastApiClient&gt; in Program.cs.
// /// BaseAddress is set there — all URLs here are relative (no leading slash needed
// /// when BaseAddress has a trailing slash).
// /// </summary>
// public class FastApiClient(HttpClient http, ILogger<FastApiClient> logger) : IFastApiClient
// {
//     private static readonly JsonSerializerOptions _json = new()
//     {
//         PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
//         PropertyNameCaseInsensitive = true,
//     };
//
//     // ── Helpers ───────────────────────────────────────────────────────────────
//
//     private async Task<T> PostAsync<T>(string path, object body, CancellationToken ct)
//     {
//         try
//         {
//             var resp = await http.PostAsJsonAsync(path, body, _json, ct);
//             if (!resp.IsSuccessStatusCode)
//             {
//                 var error = await resp.Content.ReadAsStringAsync(ct);
//                 logger.LogError("FastAPI error {Status}: {Body}", resp.StatusCode, error);
//                 throw new Exception(error);
//             }            return await resp.Content.ReadFromJsonAsync<T>(_json, ct)
//                                 ?? throw new InvalidOperationException($"Null response from POST {path}");
//         }
//         catch (HttpRequestException ex)
//         {
//             logger.LogError(ex, "HTTP error calling POST {Path}", path);
//             throw;
//         }
//     }
//
//     private async Task<T> GetAsync<T>(string path, CancellationToken ct)
//     {
//         try
//         {
//             return await http.GetFromJsonAsync<T>(path, _json, ct)
//                    ?? throw new InvalidOperationException($"Null response from GET {path}");
//         }
//         catch (HttpRequestException ex)
//         {
//             logger.LogError(ex, "HTTP error calling GET {Path}", path);
//             throw;
//         }
//     }
//
//     // ── Query ─────────────a────────────────────────────────────────────────────
//
//     public Task<QueryResponse> QueryAsync(QueryRequest request, CancellationToken ct = default)
//         => PostAsync<QueryResponse>("query", request, ct);
//
//     public Task<QueryResponse> QueryNarrateAsync(QueryRequest request, CancellationToken ct = default)
//         => PostAsync<QueryResponse>("query/narrate", request, ct);
//
//     public Task<QueryResponse> QueryExplorerAsync(QueryRequest request, CancellationToken ct = default)
//         => PostAsync<QueryResponse>("query/explore", request, ct);
//
//     // ── Streaming ─────────────────────────────────────────────────────────────
//     // IMPORTANT: do NOT use `using` on the HttpResponseMessage in stream methods.
//     // Disposing the response disposes the stream before it is fully read.
//     // The HttpClient itself manages lifetime here.
//
//     public async IAsyncEnumerable<string> QueryStreamAsync(
//         QueryRequest request,
//         [EnumeratorCancellation] CancellationToken ct = default)
//     {
//         await foreach (var token in StreamSse("query/stream", request, ct))
//             yield return token;
//     }
//
//     public async IAsyncEnumerable<string> QueryNarrateStreamAsync(
//         QueryRequest request,
//         [EnumeratorCancellation] CancellationToken ct = default)
//     {
//         await foreach (var token in StreamSse("query/narrate/stream", request, ct))
//             yield return token;
//     }
//
//     /// <summary>
//     /// Shared SSE reader. Sends a POST, reads lines, strips "data: " prefix,
//     /// stops at [DONE], skips __SOURCES__ frames (handled by caller if needed).
//     /// </summary>
//     private async IAsyncEnumerable<string> StreamSse(
//         string path,
//         object body,
//         [EnumeratorCancellation] CancellationToken ct)
//     {
//         var req = new HttpRequestMessage(HttpMethod.Post, path)
//         {
//             Content = JsonContent.Create(body, options: _json)
//         };
//
//         // ResponseHeadersRead = start reading as soon as headers arrive, don't buffer body
//         var resp = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
//         resp.EnsureSuccessStatusCode();
//
//         await using var stream = await resp.Content.ReadAsStreamAsync(ct);
//         using var reader = new StreamReader(stream);
//
//         while (!reader.EndOfStream && !ct.IsCancellationRequested)
//         {
//             var line = await reader.ReadLineAsync(ct);
//             if (line is null) yield break;
//             if (!line.StartsWith("data: ")) continue;
//
//             var payload = line["data: ".Length..];
//             if (payload == "[DONE]") yield break;
//             if (payload.StartsWith("__SOURCES__:")) continue;
//             if (payload.StartsWith("[ERROR]"))
//             {
//                 logger.LogError("FastAPI stream error: {Payload}", payload);
//                 yield return payload;
//                 yield break;
//             }
//
//             yield return payload;
//         }
//     }
//
//     // ── System ────────────────────────────────────────────────────────────────
//
//     public Task<HealthPayload> HealthAsync(CancellationToken ct = default)
//         => GetAsync<HealthPayload>("health", ct);
//
//     public Task<InfoPayload> InfoAsync(CancellationToken ct = default)
//         => GetAsync<InfoPayload>("info", ct);
//
//     // ── Sources ───────────────────────────────────────────────────────────────
//
//     public Task<SourceListPayload> ListSourcesAsync(CancellationToken ct = default)
//         => GetAsync<SourceListPayload>("sources", ct);
//
//     public Task<SourceChunksPayload> GetSourceChunksAsync(
//         string sourceName, int limit = 20, CancellationToken ct = default)
//         => GetAsync<SourceChunksPayload>(
//             $"sources/{Uri.EscapeDataString(sourceName)}?limit={limit}", ct);
//
//     // ── Memory ────────────────────────────────────────────────────────────────
//
//     public Task<MemoryPayload> GetMemoryAsync(string sessionId, CancellationToken ct = default)
//         => GetAsync<MemoryPayload>(
//             $"memory?session_id={Uri.EscapeDataString(sessionId)}", ct);
//
//     public async Task<Empty> ClearMemoryAsync(string sessionId, CancellationToken ct = default)
//     {
//         try
//         {
//             var resp = await http.DeleteAsync(
//                 $"memory?session_id={Uri.EscapeDataString(sessionId)}", ct);
//             resp.EnsureSuccessStatusCode();
//             return await resp.Content.ReadFromJsonAsync<Empty>(_json, ct)
//                    ?? new Empty { Status = "cleared" };
//         }
//         catch (HttpRequestException ex)
//         {
//             logger.LogError(ex, "HTTP error calling DELETE memory for session {SessionId}", sessionId);
//             throw;
//         }
//     }
// }