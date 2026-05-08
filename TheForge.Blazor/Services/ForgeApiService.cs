using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using TheForge.Blazor.Models;

namespace TheForge.Blazor.Services;

/// <summary>
/// Calls TheForge ASP.NET Core API (not FastAPI directly).
/// All intelligence lives in Python — this just talks to the .NET gateway.
/// </summary>
public class ForgeApiService(HttpClient http)
{
    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    // ── Sync query ────────────────────────────────────────────────────────────

    public async Task<(string response, List<SourceRef> sources)> QueryAsync(
        string query,
        ChatSettings settings,
        CancellationToken ct = default)
    {
        var endpoint = settings.Mode switch
        {
            ChatMode.Narrator => "/api/query/narrate",
            ChatMode.Explorer => "/api/query/explore",
            _                 => "/api/query",
        };

        var body = BuildBody(query, settings);
        using var req = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(body, _json),
                Encoding.UTF8, "application/json")
        };
        req.Headers.Add("X-Api-Key", settings.ApiKey);

        using var resp = await http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();

        var result = await resp.Content.ReadFromJsonAsync<QueryResponseDto>(_json, ct);
        var sources = result?.Sources?.Select(s => new SourceRef
        {
            Source = s.Source ?? string.Empty,
            Chapter = s.Chapter ?? string.Empty,
            StitchRange = s.StitchRange ?? string.Empty,
            Score = s.Score,
        }).ToList() ?? [];

        return (result?.Response ?? string.Empty, sources);
    }

    // ── Streaming query ───────────────────────────────────────────────────────

    public async IAsyncEnumerable<string> QueryStreamAsync(
        string query,
        ChatSettings settings,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken ct = default)
    {
        var endpoint = settings.Mode == ChatMode.Narrator
            ? "/api/query/narrate/stream"
            : "/api/query/stream";

        var body = BuildBody(query, settings);
        using var req = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(body, _json),
                Encoding.UTF8, "application/json")
        };
        req.Headers.Add("X-Api-Key", settings.ApiKey);

        using var resp = await http.SendAsync(req,
            HttpCompletionOption.ResponseHeadersRead, ct);
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
            if (payload.StartsWith("__SOURCES__:")) continue; // sources handled separately
            if (payload.StartsWith("[ERROR]"))
            {
                yield return $"\n\n⚠️ {payload}";
                yield break;
            }

            yield return payload;
        }
    }

    // ── Health check ──────────────────────────────────────────────────────────

    public async Task<bool> IsHealthyAsync(CancellationToken ct = default)
    {
        try
        {
            var resp = await http.GetAsync("/api/health", ct);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static object BuildBody(string query, ChatSettings settings) => new
    {
        query,
        top_k = settings.TopK,
        mode = settings.Mode.ToString().ToLowerInvariant(),
    };

    // ── DTOs (internal to this service) ──────────────────────────────────────

    private class QueryResponseDto
    {
        public string? Response { get; set; }
        public List<SourceDto>? Sources { get; set; }
        public int ChunksUsed { get; set; }
    }

    private class SourceDto
    {
        public string? Source { get; set; }
        public string? Chapter { get; set; }
        public string? StitchRange { get; set; }
        public double Score { get; set; }
    }
}
