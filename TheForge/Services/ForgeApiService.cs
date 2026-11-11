using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TheForge.Models;
using TheForge.Services.Interfaces;

namespace TheForge.Services;

/// <summary>
/// Blazor's API surface. Sync methods share the typed FastAPI client.
/// Narrator returns the actual response stream to Blazor for event parsing.
/// No LLM generation or retrieval is performed inside TheForge.
/// </summary>
public sealed class ForgeApiService : IForgeApiService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    private readonly IFastApiClient _api;
    private readonly ILogger<ForgeApiService> _logger;
    private readonly HttpClient _streamClient;

    public ForgeApiService(
        IFastApiClient api,
        IHttpClientFactory httpClients,
        IConfiguration configuration,
        ILogger<ForgeApiService> logger)
    {
        _api = api;
        _logger = logger;
        _streamClient = httpClients.CreateClient("ForgeApi");
        var baseUrl = configuration["FastApi:BaseUrl"] ?? "http://localhost:8000/";
        if (!baseUrl.EndsWith('/')) baseUrl += "/";
        _streamClient.BaseAddress = new Uri(baseUrl);
    }

    public async Task<HealthPayload?> GetHealthAsync(CancellationToken ct = default)
    {
        try { return await _api.HealthAsync(ct); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "FastAPI health request failed.");
            return new HealthPayload { Status = "error" };
        }
    }

    public async Task<SourceListPayload?> GetSourcesAsync(CancellationToken ct = default)
    {
        try { return await _api.ListSourcesAsync(ct); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "FastAPI sources listing is unavailable.");
            return new SourceListPayload();
        }
    }

    public async Task<BookListPayload?> GetBooksAsync(CancellationToken ct = default)
    {
        try { return await _api.GetBooksAsync(ct); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "FastAPI book manifest is unavailable.");
            return new BookListPayload();
        }
    }

    public async Task<QueryResponse?> QueryExploreAsync(QueryRequest query, CancellationToken ct = default)
        => await _api.QueryExplorerAsync(query, ct);

    public async Task<QueryResponse?> QueryFocusAsync(QueryRequest query, CancellationToken ct = default)
        => await _api.QueryFocusAsync(query, ct);

    public async Task<HttpResponseMessage> BeginNarrateStreamAsync(
        QueryRequest query, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "query/narrate")
        {
            Content = JsonContent.Create(query, options: JsonOptions)
        };
        return await _streamClient.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, ct);
    }
}
