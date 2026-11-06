using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Components;
using TheForge.Models;
using TheForge.Services.Interfaces;

namespace TheForge.Services;

/// <summary>HTTP transport only. Callers decide how to present failures and cancellation.</summary>
public sealed class ForgeApiService(IHttpClientFactory httpClientFactory, NavigationManager navigation) : IForgeApiService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public Task<HealthPayload?> GetHealthAsync(CancellationToken ct = default) => GetAsync<HealthPayload>("api/health", ct);
    public Task<SourceListPayload?> GetSourcesAsync(CancellationToken ct = default) => GetAsync<SourceListPayload>("api/sources", ct);
    public Task<BookListPayload?> GetBooksAsync(CancellationToken ct = default) => GetAsync<BookListPayload>("api/books", ct);
    public Task<QueryResponse?> QueryExploreAsync(QueryRequest request, CancellationToken ct = default) => QueryAsync("api/query/explore", request, ct);
    public Task<QueryResponse?> QueryFocusAsync(QueryRequest request, CancellationToken ct = default) => QueryAsync("api/query/focus", request, ct);

    public async Task<HttpResponseMessage> BeginNarrateStreamAsync(QueryRequest request, CancellationToken ct = default)
    {
        var client = CreateClient();
        using var message = new HttpRequestMessage(HttpMethod.Post, "api/query/narrate")
        {
            Content = JsonContent.Create(request, options: JsonOptions)
        };
        return await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, ct);
    }

    private async Task<T?> GetAsync<T>(string path, CancellationToken ct)
    {
        using var client = CreateClient();
        return await client.GetFromJsonAsync<T>(path, JsonOptions, ct);
    }

    private async Task<QueryResponse?> QueryAsync(string path, QueryRequest request, CancellationToken ct)
    {
        using var client = CreateClient();
        using var response = await client.PostAsJsonAsync(path, request, JsonOptions, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<QueryResponse>(JsonOptions, ct);
    }

    private HttpClient CreateClient()
    {
        var client = httpClientFactory.CreateClient("ForgeApi");
        client.BaseAddress ??= new Uri(navigation.BaseUri);
        return client;
    }
}
