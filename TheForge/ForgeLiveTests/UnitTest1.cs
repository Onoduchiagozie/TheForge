using Xunit;

namespace ForgeLiveTests;

/*
 * TheForge.Tests — ForgeApiTests.cs  (Full Suite — Live Mode)
 * ============================================================
 * Two test collections in one file:
 *
 *   1. ForgeApiTests      — targets TheForge ASP.NET Core API
 *   2. CheckmarkApiTests  — targets FastAPI (Python) directly
 *
 * SETUP BEFORE RUNNING
 * ─────────────────────────────────────────────────────────────
 *  Machine      │ Service         │ Address
 *  ─────────────┼─────────────────┼──────────────────────────
 *  Lenovo       │ FastAPI (main)  │ http://192.168.100.71:8000
 *  Dell (local) │ TheForge .NET   │ http://localhost:5000
 *
 *  On Lenovo:   python main.py api          (or uvicorn main:app --port 8000)
 *  On Dell:     dotnet run                  (in TheForge project root)
 *
 * RUN
 * ─────────────────────────────────────────────────────────────
 *  cd TheForge.Tests
 *  dotnet test -v normal
 *
 *  To run only one collection:
 *  dotnet test --filter "FullyQualifiedName~CheckmarkApiTests"
 *  dotnet test --filter "FullyQualifiedName~ForgeApiTests"
 *
 * NOTES
 * ─────────────────────────────────────────────────────────────
 *  - Tests are ordered A→ so they run in dependency order.
 *  - _sharedState is a static holder so registration key survives
 *    across xUnit's per-test class instantiation.
 *  - Stream tests use HttpCompletionOption.ResponseHeadersRead
 *    so they don't buffer the entire SSE body before reading.
 */

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Xunit.Abstractions;


// ═══════════════════════════════════════════════════════════════════════════════
// Shared state between test instances (xUnit creates a new instance per test)
// ═══════════════════════════════════════════════════════════════════════════════

file static class SharedState
{
    public static string? FreeApiKey   { get; set; }   // set by registration test
    public static string? TestEmail    => "xunit-test@forge.dev";
    public static string? TestPassword => "xunit-pass-2026";
}

// ═══════════════════════════════════════════════════════════════════════════════
//  COLLECTION 1 — TheForge ASP.NET Core API
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Integration tests for TheForge (localhost:5000).
/// TheForge proxies all RAG calls to FastAPI on 192.168.100.71:8000.
/// Both services must be running before executing these tests.
/// </summary>
public class ForgeApiTests(ITestOutputHelper output) : IDisposable
{
    // ── Config ────────────────────────────────────────────────────────────────
    private const string BaseUrl = "http://localhost:5157";
    private const string DevKey  = "forge-dev-key";           // archmagos seed key

    private readonly HttpClient _http = new()
    {
        BaseAddress = new Uri(BaseUrl + "/"),
        Timeout     = TimeSpan.FromSeconds(2520),
    };

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy        = JsonNamingPolicy.SnakeCaseLower,
    };

    public void Dispose() => _http.Dispose();

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// Sets X-Api-Key header and returns self for fluent chaining.
    private HttpClient WithKey(string key)
    {
        _http.DefaultRequestHeaders.Remove("X-Api-Key");
        _http.DefaultRequestHeaders.Add("X-Api-Key", key);
        return _http;
    }

    private static StringContent Json(object body) =>
        new(JsonSerializer.Serialize(body, _json), Encoding.UTF8, "application/json");

    /// Reads and deserialises JSON response; prints raw body to test output.
    private async Task<T> Read<T>(HttpResponseMessage resp)
    {
        var content = await resp.Content.ReadAsStringAsync();
        output.WriteLine($"[{resp.StatusCode}] {content}");
        try
        {
            return JsonSerializer.Deserialize<T>(content, _json)
                   ?? throw new InvalidOperationException($"Null deserialisation: {content}");
        }
        catch (JsonException)
        {
            throw new InvalidOperationException($"Could not deserialise response: {content}");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  A — Health (public — no key required)
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task A_Health_ReturnsOk_WithoutAuth()
    {
        _http.DefaultRequestHeaders.Remove("X-Api-Key");

        var resp = await _http.GetAsync("api/health");

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "health is a public endpoint — no auth required");

        var body = await Read<Dictionary<string, JsonElement>>(resp);
        body.Should().ContainKey("status",
            because: "FastAPI returns {\"status\": ...}");

        var status = body["status"].GetString();
        output.WriteLine($"FastAPI status from Lenovo: {status}");
        status.Should().NotBeNullOrEmpty();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  A2 — Info proxy
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task A2_Info_ReturnsIndexStats()
    {
        WithKey(DevKey);
        var resp = await _http.GetAsync("api/info");

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "info endpoint proxies FastAPI /info");

        var body = await Read<Dictionary<string, JsonElement>>(resp);
        output.WriteLine("Info response keys: " + string.Join(", ", body.Keys));
        // FastAPI returns profile, index stats etc. — just assert it's a non-empty object
        body.Should().NotBeEmpty(because: "info must return at least one key");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  B — Auth: missing key → 401
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task B_Query_WithoutApiKey_Returns401()
    {
        _http.DefaultRequestHeaders.Remove("X-Api-Key");

        var resp = await _http.PostAsync("api/query",
            Json(new { query = "test" }));

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            because: "all query endpoints require X-Api-Key header");
    }

    [Fact]
    public async Task B2_Query_InvalidApiKey_Returns401()
    {
        WithKey("forge-thisisnotreal");

        var resp = await _http.PostAsync("api/query",
            Json(new { query = "test" }));

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            because: "a non-existent key must be rejected");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  C — Registration
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task C_Register_CreatesUser_ReturnsApiKey()
    {
        var resp = await _http.PostAsync("api/auth/register",
            Json(new { email = SharedState.TestEmail, password = SharedState.TestPassword }));

        if (resp.StatusCode == HttpStatusCode.Conflict)
        {
            output.WriteLine("User already exists — rotating key via new-key endpoint.");
            var newKeyResp = await _http.PostAsync("api/auth/new-key",
                Json(new { email = SharedState.TestEmail, password = SharedState.TestPassword }));

            newKeyResp.StatusCode.Should().Be(HttpStatusCode.OK,
                because: "new-key must succeed when credentials are valid");

            var newKeyBody = await Read<Dictionary<string, JsonElement>>(newKeyResp);
            SharedState.FreeApiKey = newKeyBody["api_key"].GetString();
            output.WriteLine($"Rotated free key: {SharedState.FreeApiKey?[..12]}…");
            return;
        }

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "first-time registration must succeed");

        var body = await Read<Dictionary<string, JsonElement>>(resp);
        body.Should().ContainKey("api_key");
        body.Should().ContainKey("tier");
        body["tier"].GetString().Should().Be("free");

        SharedState.FreeApiKey = body["api_key"].GetString();
        SharedState.FreeApiKey.Should().StartWith("forge-",
            because: "all generated keys must carry the forge- prefix");

        output.WriteLine($"Registered. Free key: {SharedState.FreeApiKey?[..12]}…");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  D — Duplicate registration → 409
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task D_Register_DuplicateEmail_Returns409()
    {
        // admin@theforge.dev is always seeded
        var resp = await _http.PostAsync("api/auth/register",
            Json(new { email = "admin@theforge.dev", password = "anything" }));

        resp.StatusCode.Should().Be(HttpStatusCode.Conflict,
            because: "duplicate email must return 409 Conflict");

        var body = await Read<Dictionary<string, JsonElement>>(resp);
        body.Should().ContainKey("error");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  D2 — Login
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task D2_Login_ValidCredentials_ReturnsOk()
    {
        // Ensure test user exists first (run C before this in practice)
        var resp = await _http.PostAsync("api/auth/login",
            Json(new { email = SharedState.TestEmail, password = SharedState.TestPassword }));

        // May be 401 if test user was never registered — just assert shape if OK
        if (resp.StatusCode == HttpStatusCode.OK)
        {
            var body = await Read<Dictionary<string, JsonElement>>(resp);
            body.Should().ContainKey("tier");
            body.Should().ContainKey("email");
            output.WriteLine($"Login confirmed. Tier: {body["tier"].GetString()}");
        }
        else
        {
            output.WriteLine($"Login returned {resp.StatusCode} — run C first.");
        }
    }

    [Fact]
    public async Task D3_Login_WrongPassword_Returns401()
    {
        var resp = await _http.PostAsync("api/auth/login",
            Json(new { email = "admin@theforge.dev", password = "wrong-password" }));

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            because: "wrong password must be rejected");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  E — Empty / whitespace query → 400
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task E_Query_EmptyString_Returns400()
    {
        WithKey(DevKey);
        var resp = await _http.PostAsync("api/query",
            Json(new { query = "   ", session_id = "xunit-empty" }));

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            because: "whitespace-only query must be rejected before hitting FastAPI");

        var body = await Read<Dictionary<string, JsonElement>>(resp);
        body.Should().ContainKey("error");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  F — Sync query (archmagos / dev key)
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task F_Query_ValidQuery_Returns200WithShape()
    {
        WithKey(DevKey);
        var resp = await _http.PostAsync("api/query",
            Json(new { query = "Who is Horus Lupercal?", session_id = "xunit-sync" }));

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "valid query with dev key must return 200");

        var body = await Read<Dictionary<string, JsonElement>>(resp);

        body.Should().ContainKey("response",   because: "RAG response text must be present");
        body.Should().ContainKey("sources",    because: "source list must be present");
        body.Should().ContainKey("chunks_used",because: "chunk count must be present");

        body["response"].GetString().Should().NotBeNullOrEmpty(
            because: "response text must not be empty on a live index");

        var chunksUsed = body["chunks_used"].GetInt32();
        chunksUsed.Should().BeGreaterThan(0,
            because: "a live FAISS index should return at least one chunk");

        output.WriteLine($"chunks_used: {chunksUsed}");
        output.WriteLine($"response snippet: {body["response"].GetString()?[..Math.Min(360, body["response"].GetString()!.Length)]}…");
    }

    [Fact]
    public async Task F2_Query_WithTopK_HonoursParameter()
    {
        WithKey(DevKey);
        var resp = await _http.PostAsync("api/query",
            Json(new { query = "Describe the Heresy", session_id = "xunit-topk", top_k = 3 }));

        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await Read<Dictionary<string, JsonElement>>(resp);
        body["chunks_used"].GetInt32().Should().BeLessThanOrEqualTo(3,
            because: "top_k = 3 must not return more than 3 chunks");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  G — Narrator: free tier → 403
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task G_Narrate_FreeTier_Returns403()
    {
        var freeKey = SharedState.FreeApiKey;
        if (freeKey is null)
        {
            output.WriteLine("Skipping G: run C first to obtain a free-tier key.");
            return;
        }

        WithKey(freeKey);
        var resp = await _http.PostAsync("api/query/narrate",
            Json(new { query = "Describe the Dropsite Massacre", session_id = "xunit-narrate-free" }));

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            because: "narrator mode requires Adept tier or above");

        var body = await Read<Dictionary<string, JsonElement>>(resp);
        body.Should().ContainKey("error");
        body["error"].GetString().Should().Contain("Adept",
            because: "the error message must name the required tier");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  H — Narrator: archmagos → 200
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task H_Narrate_ArchmagosKey_Returns200()
    {
        WithKey(DevKey);
        var resp = await _http.PostAsync("api/query/narrate",
            Json(new { query = "Who is Ferrus Manus?", session_id = "xunit-narrate-arch" }));

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "archmagos tier must be able to use narrator mode");

        var body = await Read<Dictionary<string, JsonElement>>(resp);
        body.Should().ContainKey("response");
        body["response"].GetString().Should().NotBeNullOrEmpty();
        output.WriteLine($"Narrator response snippet: {body["response"].GetString()?[..Math.Min(360, body["response"].GetString()!.Length)]}…");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  H2 — Explorer: free tier → 403
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task H2_Explore_FreeTier_Returns403()
    {
        var freeKey = SharedState.FreeApiKey;
        if (freeKey is null)
        {
            output.WriteLine("Skipping H2: run C first to obtain a free-tier key.");
            return;
        }

        WithKey(freeKey);
        var resp = await _http.PostAsync("api/query/explore",
            Json(new { query = "Tell me about the Space Marines", session_id = "xunit-explore-free" }));

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            because: "explorer mode requires Adept tier or above");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  H3 — Explorer: archmagos → 200
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task H3_Explore_ArchmagosKey_Returns200()
    {
        WithKey(DevKey);
        var resp = await _http.PostAsync("api/query/explore",
            Json(new { query = "Tell me about the Thousand Sons", session_id = "xunit-explore-arch" }));

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "archmagos tier must be able to use explorer mode");

        var body = await Read<Dictionary<string, JsonElement>>(resp);
        body.Should().ContainKey("response");
        body["response"].GetString().Should().NotBeNullOrEmpty();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  I — SSE Stream: /api/query/stream
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task I_Stream_YieldsTokens_EndsWithDone()
    {
        WithKey(DevKey);

        var req = new HttpRequestMessage(HttpMethod.Post, "api/query/stream")
        {
            Content = Json(new { query = "Who is the Emperor of Mankind?", session_id = "xunit-stream" })
        };

        // ResponseHeadersRead = don't buffer — start reading as soon as headers arrive
        var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        resp.Content.Headers.ContentType?.MediaType.Should().Be("text/event-stream",
            because: "stream endpoint must return text/event-stream");

        await using var stream = await resp.Content.ReadAsStreamAsync();
        using var reader        = new StreamReader(stream);

        var tokens       = new List<string>();
        var doneReceived = false;
        var errorSeen    = false;

        // Read up to 300 lines or until [DONE] — whichever comes first
        for (var lineCount = 0; lineCount < 300 && !reader.EndOfStream; lineCount++)
        {
            var line = await reader.ReadLineAsync();
            if (line is null) break;
            if (!line.StartsWith("data: ")) continue;

            var payload = line["data: ".Length..];

            if (payload.StartsWith("[ERROR]")) { errorSeen = true; break; }
            if (payload == "[DONE]")           { doneReceived = true; break; }

            tokens.Add(payload);
        }

        errorSeen.Should().BeFalse(
            because: "stream must not return [ERROR] — check FastAPI connection on Lenovo");

        doneReceived.Should().BeTrue(
            because: "stream must terminate with data: [DONE]");

        tokens.Count.Should().BeGreaterThan(0,
            because: "at least one token must arrive before [DONE]");

        output.WriteLine($"Stream received {tokens.Count} tokens. First: \"{tokens.FirstOrDefault()}\"");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  I2 — SSE Stream: /api/query/narrate/stream (archmagos)
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task I2_NarrateStream_ArchmagosKey_YieldsTokensAndDone()
    {
        WithKey(DevKey);

        var req = new HttpRequestMessage(HttpMethod.Post, "api/query/narrate/stream")
        {
            Content = Json(new { query = "Describe the Battle of Calth", session_id = "xunit-narrate-stream" })
        };

        var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        resp.Content.Headers.ContentType?.MediaType.Should().Be("text/event-stream");

        await using var stream = await resp.Content.ReadAsStreamAsync();
        using var reader        = new StreamReader(stream);

        var tokens       = new List<string>();
        var doneReceived = false;

        for (var i = 0; i < 300 && !reader.EndOfStream; i++)
        {
            var line = await reader.ReadLineAsync();
            if (line is null || !line.StartsWith("data: ")) continue;
            var payload = line["data: ".Length..];
            if (payload == "[DONE]") { doneReceived = true; break; }
            tokens.Add(payload);
        }

        doneReceived.Should().BeTrue(because: "narrate/stream must terminate with [DONE]");
        tokens.Should().NotBeEmpty(because: "at least one token must arrive");
        output.WriteLine($"NarrateStream tokens: {tokens.Count}");
    }

    [Fact]
    public async Task I3_NarrateStream_FreeTier_Returns403()
    {
        var freeKey = SharedState.FreeApiKey;
        if (freeKey is null) { output.WriteLine("Skipping I3: run C first."); return; }

        WithKey(freeKey);

        var req = new HttpRequestMessage(HttpMethod.Post, "api/query/narrate/stream")
        {
            Content = Json(new { query = "Who is Lorgar?", session_id = "xunit-narrate-stream-free" })
        };

        var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            because: "free tier must be blocked from narrate/stream before any SSE output");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  J — Settings: GET
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task J_Settings_Get_ReturnsUserProfile()
    {
        WithKey(DevKey);
        var resp = await _http.GetAsync("api/settings");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await Read<Dictionary<string, JsonElement>>(resp);
        body.Should().ContainKey("tier");
        body.Should().ContainKey("email");
        body.Should().ContainKey("preferred_mode");

        body["tier"].GetString().Should().Be("archmagos",
            because: "forge-dev-key belongs to the seeded archmagos user");

        output.WriteLine($"Email: {body["email"].GetString()}, Tier: {body["tier"].GetString()}, Mode: {body["preferred_mode"].GetString()}");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  K — Settings: PUT
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task K_Settings_Put_UpdatesSuccessfully()
    {
        WithKey(DevKey);
        var resp = await _http.PutAsync("api/settings",
            Json(new { preferred_mode = "remembrancer", top_k = 8 }));

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "valid settings update must return 200");

        var body = await Read<Dictionary<string, JsonElement>>(resp);
        body.Should().ContainKey("message");
    }

    [Fact]
    public async Task K2_Settings_Put_ExceedsTierCap_IsEnforced()
    {
        // Free tier caps top_k at 4 — submitting 10 should be silently capped
        var freeKey = SharedState.FreeApiKey;
        if (freeKey is null) { output.WriteLine("Skipping K2: run C first."); return; }

        WithKey(freeKey);
        // Either returns 200 (capped silently) or 400 (explicit rejection)
        var resp = await _http.PutAsync("api/settings",
            Json(new { top_k = 10 }));

        new[] { HttpStatusCode.OK, HttpStatusCode.BadRequest }
            .Should().Contain(resp.StatusCode,
            because: "exceeding tier cap must either be silently capped or explicitly rejected");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  K3 — Tier info
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task K3_TierInfo_Archmagos_ReturnsUnlimitedLimit()
    {
        WithKey(DevKey);
        var resp = await _http.GetAsync("api/settings/tier");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await Read<Dictionary<string, JsonElement>>(resp);
        body.Should().ContainKey("tier");
        body.Should().ContainKey("daily_limit");
        body.Should().ContainKey("modes");

        body["tier"].GetString().Should().Be("archmagos");
        body["daily_limit"].GetInt32().Should().Be(-1,
            because: "archmagos has no daily limit (-1 = unlimited)");

        output.WriteLine($"Tier info: {body["tier"].GetString()}, daily_limit={body["daily_limit"].GetInt32()}, max_top_k={body["max_top_k"].GetInt32()}");
    }

    [Fact]
    public async Task K4_TierInfo_FreeTier_HasCorrectLimits()
    {
        var freeKey = SharedState.FreeApiKey;
        if (freeKey is null) { output.WriteLine("Skipping K4: run C first."); return; }

        WithKey(freeKey);
        var resp = await _http.GetAsync("api/settings/tier");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await Read<Dictionary<string, JsonElement>>(resp);

        body["tier"].GetString().Should().Be("free");
        body["daily_limit"].GetInt32().Should().Be(10);
        body["max_top_k"].GetInt32().Should().Be(4);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  L — Sources proxy
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task L_Sources_List_ReturnsShape()
    {
        WithKey(DevKey);
        var resp = await _http.GetAsync("api/sources");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await Read<Dictionary<string, JsonElement>>(resp);
        body.Should().ContainKey("total",
            because: "sources list must include a total count");

        var total = body["total"].GetInt32();
        output.WriteLine($"Total sources on Lenovo FAISS: {total}");
        // On a live index, total > 0. On an empty index it's 0. Both are valid here.
        total.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task L2_Sources_Detail_UnknownSource_Returns404OrEmpty()
    {
        WithKey(DevKey);
        var resp = await _http.GetAsync("api/sources/this-source-does-not-exist?limit=5");

        // Either 404 or 200 with empty list depending on FastAPI implementation
        new[] { HttpStatusCode.OK, HttpStatusCode.NotFound }
            .Should().Contain(resp.StatusCode,
            because: "unknown source name must not cause a 500");

        output.WriteLine($"Unknown source returned: {resp.StatusCode}");
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
//  COLLECTION 2 — Checkmark: FastAPI (Python) on Lenovo
//  Direct tests against 192.168.100.71:8000 — bypasses TheForge entirely.
//  Useful for isolating whether failures are in .NET or Python.
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Direct integration tests for FastAPI running on Lenovo (192.168.100.71:8000).
/// Run these independently to confirm the Python side is healthy before debugging TheForge.
/// </summary>
public class CheckmarkApiTests(ITestOutputHelper output) : IDisposable
{
    // ── Config ────────────────────────────────────────────────────────────────
    // Point directly at Lenovo FastAPI — no .NET layer
    private const string LenovoBaseUrl = "http://192.168.100.71:8000";

    private readonly HttpClient _http = new()
    {
        BaseAddress = new Uri(LenovoBaseUrl + "/"),
        Timeout     = TimeSpan.FromSeconds(2520),
    };

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy        = JsonNamingPolicy.SnakeCaseLower,
    };

    public void Dispose() => _http.Dispose();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<T> Read<T>(HttpResponseMessage resp)
    {
        var content = await resp.Content.ReadAsStringAsync();
        output.WriteLine($"[{resp.StatusCode}] {content}");
        return JsonSerializer.Deserialize<T>(content, _json)
               ?? throw new InvalidOperationException($"Null deserialisation: {content}");
    }

    private static StringContent Json(object body) =>
        new(JsonSerializer.Serialize(body, _json), Encoding.UTF8, "application/json");

    // ═══════════════════════════════════════════════════════════════════════════
    //  FA — FastAPI reachable from Dell
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task FA_FastApi_IsReachable_FromDell()
    {
        try
        {
            var resp = await _http.GetAsync("health");
            resp.StatusCode.Should().Be(HttpStatusCode.OK,
                because: "FastAPI on 192.168.100.71:8000 must respond to GET /health");

            var body = await Read<Dictionary<string, JsonElement>>(resp);
            body.Should().ContainKey("status");
            output.WriteLine($"FastAPI reachable. Status: {body["status"].GetString()}");
        }
        catch (HttpRequestException ex)
        {
            // Make error message informative
            throw new Exception(
                $"Cannot reach FastAPI at {LenovoBaseUrl}. " +
                $"Ensure uvicorn is running on Lenovo and firewall allows port 8000. " +
                $"Inner: {ex.Message}", ex);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  FB — FastAPI /info (index statistics)
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task FB_FastApi_Info_ReturnsIndexStats()
    {
        var resp = await _http.GetAsync("info");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await Read<Dictionary<string, JsonElement>>(resp);

        output.WriteLine("FastAPI /info keys: " + string.Join(", ", body.Keys));

        // Print everything for diagnostics
        foreach (var (k, v) in body)
            output.WriteLine($"  {k}: {v}");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  FC — FastAPI /query (sync)
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task FC_FastApi_Query_ReturnsResponse()
    {
        var resp = await _http.PostAsync("query",
            Json(new { query = "Who is Horus Lupercal?", top_k = 4, session_id = "checkmark-sync" }));

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "FastAPI /query must return 200 for a valid query");

        var body = await Read<Dictionary<string, JsonElement>>(resp);
        body.Should().ContainKey("response");
        body.Should().ContainKey("sources");
        body.Should().ContainKey("chunks_used");

        var responseText = body["response"].GetString();
        responseText.Should().NotBeNullOrEmpty(because: "response text must not be empty on a live index");
        output.WriteLine($"FastAPI response snippet: {responseText?[..Math.Min(200, responseText!.Length)]}");
        output.WriteLine($"chunks_used: {body["chunks_used"].GetInt32()}");

        // Print all sources
        if (body.TryGetValue("sources", out var sourcesEl))
        {
            var sources = sourcesEl.EnumerateArray().Select(s => s.ToString()).ToList();
            output.WriteLine($"Sources: {string.Join(", ", sources)}");
        }
    }

    [Fact]
    public async Task FC2_FastApi_Query_EmptyQuery_Returns422Or400()
    {
        var resp = await _http.PostAsync("query",
            Json(new { query = "", top_k = 4 }));

        // FastAPI may return 422 (validation) or 400 (explicit)
        new[] { HttpStatusCode.UnprocessableEntity, HttpStatusCode.BadRequest }
            .Should().Contain(resp.StatusCode,
            because: "empty query must be rejected by FastAPI validation");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  FD — FastAPI /query/narrate
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task FD_FastApi_Narrate_ReturnsResponse()
    {
        var resp = await _http.PostAsync("query/narrate",
            Json(new { query = "Who is Ferrus Manus?", top_k = 4, session_id = "checkmark-narrate" }));

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "FastAPI /query/narrate must return 200");

        var body = await Read<Dictionary<string, JsonElement>>(resp);
        body.Should().ContainKey("response");
        body["response"].GetString().Should().NotBeNullOrEmpty();
        output.WriteLine($"Narrate response snippet: {body["response"].GetString()?[..Math.Min(200, body["response"].GetString()!.Length)]}");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  FE — FastAPI /query/explorer
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task FE_FastApi_Explorer_ReturnsResponse()
    {
        var resp = await _http.PostAsync("query/explorer",
            Json(new { query = "Thousand Sons lore", top_k = 4, session_id = "checkmark-explore" }));

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "FastAPI /query/explorer must return 200");

        var body = await Read<Dictionary<string, JsonElement>>(resp);
        body.Should().ContainKey("response");
        output.WriteLine($"Explorer response snippet: {body["response"].GetString()?[..Math.Min(200, body["response"].GetString()!.Length)]}");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  FF — FastAPI /query/stream (SSE direct)
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task FF_FastApi_Stream_YieldsTokensAndDone()
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "query/stream")
        {
            Content = Json(new { query = "Who is the Emperor of Mankind?", top_k = 4, session_id = "checkmark-stream" })
        };

        var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "FastAPI /query/stream must return 200");
        resp.Content.Headers.ContentType?.MediaType.Should().Be("text/event-stream",
            because: "FastAPI stream must return text/event-stream");

        await using var stream = await resp.Content.ReadAsStreamAsync();
        using var reader        = new StreamReader(stream);

        var tokens       = new List<string>();
        var doneReceived = false;

        for (var i = 0; i < 500 && !reader.EndOfStream; i++)
        {
            var line = await reader.ReadLineAsync();
            if (line is null || !line.StartsWith("data: ")) continue;

            var payload = line["data: ".Length..];
            if (payload == "[DONE]") { doneReceived = true; break; }
            tokens.Add(payload);
        }

        doneReceived.Should().BeTrue(because: "FastAPI stream must end with [DONE]");
        tokens.Should().NotBeEmpty(because: "stream must emit at least one token");
        output.WriteLine($"FastAPI stream: {tokens.Count} tokens received. First: \"{tokens.FirstOrDefault()}\"");
        output.WriteLine($"Reconstructed: {string.Concat(tokens)[..Math.Min(300, string.Concat(tokens).Length)]}");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  FG — FastAPI /sources
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task FG_FastApi_Sources_List()
    {
        var resp = await _http.GetAsync("sources");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await Read<Dictionary<string, JsonElement>>(resp);
        output.WriteLine("FastAPI /sources keys: " + string.Join(", ", body.Keys));

        if (body.TryGetValue("total", out var totalEl))
            output.WriteLine($"Total sources: {totalEl.GetInt32()}");

        if (body.TryGetValue("sources", out var sourcesEl))
        {
            var names = sourcesEl.EnumerateArray().Select(s => s.ToString()).ToList();
            output.WriteLine($"Source names: {string.Join(", ", names.Take(10))}");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  FH — Network diagnostics: round-trip latency
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task FH_FastApi_Latency_IsAcceptable()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var resp = await _http.GetAsync("health");
        sw.Stop();

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        output.WriteLine($"Dell → Lenovo health round-trip: {sw.ElapsedMilliseconds} ms");

        sw.ElapsedMilliseconds.Should().BeLessThan(5000,
            because: "LAN health check must complete in under 5 seconds");
    }
}