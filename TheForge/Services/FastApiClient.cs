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
        using var response = await httpClient.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        // Each SSE event may have more than one data: line. Collect until
        // the empty line delimiter rather than corrupting paragraph tokens.
        var lines = new List<string>();
        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line is null)
                break;

            if (line.Length > 0)
            {
                if (line.StartsWith("data:", StringComparison.Ordinal))
                {
                    var value = line["data:".Length..];
                    lines.Add(value.StartsWith(' ') ? value[1..] : value);
                }
                continue;
            }

            if (lines.Count == 0)
                continue;
            var payload = string.Join("\n", lines);
            lines.Clear();

            // The upstream, not the gateway, decides whether a request
            // finished successfully. Never invent a successful [DONE].
            if (payload == "[DONE]")
            {
                yield return payload;
                yield break;
            }

            if (payload is "__STAGE__:failed" or "__STAGE__:error")
            {
                logger.LogWarning("FastAPI reported failed query stage.");
                yield return "[ERROR] Oracle reported retrieval or generation failure.";
                yield break;
            }

            if (payload.StartsWith("[ERROR]", StringComparison.Ordinal))
            {
                logger.LogError("FastAPI stream returned error frame: {Payload}", payload);
                yield return payload;
                yield break;
            }

            yield return payload;
        }

        // An upstream disconnect without [DONE] is an error, even when
        // sources or some answer tokens already arrived.
        if (!ct.IsCancellationRequested)
        {
            logger.LogWarning("FastAPI SSE connection ended before [DONE].");
            yield return "[ERROR] Oracle stream ended before completion.";
        }
    }

}