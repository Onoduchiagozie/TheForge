using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using TheForge.Models;

namespace TheForge.Services;

public class FastApiClient(HttpClient httpClient, ILogger<FastApiClient> logger) : IFastApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public Task<QueryResponse> QueryAsync(QueryRequest request, CancellationToken ct = default) =>
        PostAsync<QueryResponse>("query", request, ct);

    public Task<QueryResponse> QueryNarrateAsync(QueryRequest request, CancellationToken ct = default) =>
        PostAsync<QueryResponse>("query/narrate", request, ct);

    public Task<QueryResponse> QueryExplorerAsync(QueryRequest request, CancellationToken ct = default) =>
        PostAsync<QueryResponse>("query/explore", request, ct);

    public Task<QueryResponse> QueryFocusAsync(QueryRequest request, CancellationToken ct = default) =>
        PostAsync<QueryResponse>("query/focus", request, ct);

    public IAsyncEnumerable<string> QueryStreamAsync(QueryRequest request, CancellationToken ct = default) =>
        StreamAsync("query/stream", request, ct);

    public IAsyncEnumerable<string> QueryNarrateStreamAsync(QueryRequest request, CancellationToken ct = default) =>
        StreamAsync("query/narrate", request, ct);

    public Task<HealthPayload> HealthAsync(CancellationToken ct = default) =>
        GetAsync<HealthPayload>("health", ct);

    public Task<InfoPayload> InfoAsync(CancellationToken ct = default) =>
        GetAsync<InfoPayload>("info", ct);

    public Task<SourceListPayload> ListSourcesAsync(CancellationToken ct = default) =>
        GetAsync<SourceListPayload>("sources", ct);

    public Task<BookListPayload> GetBooksAsync(CancellationToken ct = default) =>
        GetAsync<BookListPayload>("books", ct);

    public Task<SourceChunksPayload> GetSourceChunksAsync(string sourceName, int limit = 20, CancellationToken ct = default) =>
        GetAsync<SourceChunksPayload>($"sources/{Uri.EscapeDataString(sourceName)}?limit={limit}", ct);

    private async Task<T> GetAsync<T>(string path, CancellationToken ct)
    {
        try
        {
            return await httpClient.GetFromJsonAsync<T>(path, JsonOptions, ct)
                ?? throw new InvalidOperationException($"FastAPI returned no body for GET {path}.");
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "FastAPI GET failed for {Path}", path);
            throw;
        }
    }

    private async Task<T> PostAsync<T>(string path, object body, CancellationToken ct)
    {
        try
        {
            using var response = await httpClient.PostAsJsonAsync(path, body, JsonOptions, ct);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct)
                ?? throw new InvalidOperationException($"FastAPI returned no body for POST {path}.");
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "FastAPI POST failed for {Path}", path);
            throw;
        }
    }

    private async IAsyncEnumerable<string> StreamAsync(
        string path,
        object body,
        [EnumeratorCancellation] CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body, options: JsonOptions)
        };

        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        // The parser emits complete SSE events and rejects premature EOF.
        // It preserves [DONE], [ERROR], source and stage frames rather than
        // silently converting a broken connection into apparent success.
        await foreach (var payload in SseFrameParser.ReadAsync(reader, ct))
        {
            if (payload.StartsWith("[ERROR]", StringComparison.Ordinal))
            {
                logger.LogError("FastAPI stream returned an error frame: {Payload}", payload);
            }

            yield return payload;
        }
    }
}