using Microsoft.AspNetCore.Mvc;
using TheForge.Data.Entities;
using TheForge.Models;
using TheForge.Servcies;
using TheForge.Services;

namespace TheForge.Controllers;

[ApiController]
[Route("api/query")]
public class QueryController(
    IFastApiClient fastApi,
    UserSettingsService settingsService,
    UsageService usageService,
    QueryCacheService cacheService,
    SessionService sessionService,
    ILogger<QueryController> logger) : ControllerBase
{
    private User? ForgeUser => HttpContext.Items["ForgeUser"] as User;

    // Applies tier caps and injects session_id into the request
    private IActionResult? PrepareRequest(QueryRequest req)
    {
        var user = ForgeUser;
        if (user is null) return Unauthorized(new { error = "Not authenticated." });
        settingsService.ApplyToRequest(req, user);
        req.SessionId = sessionService.GetOrCreate(user.Id, user.Tier);
        return null;
    }

    // ── POST /api/query ───────────────────────────────────────────────────────

    [HttpPost]
    public async Task<IActionResult> Query([FromBody] QueryRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
            return BadRequest(new { error = "Query cannot be empty." });

        var guard = PrepareRequest(request);
        if (guard is not null) return guard;

        var user = ForgeUser!;

        // Cache check
        var cached = cacheService.Get(request);
        if (cached is not null) return Ok(cached);

        if (!await usageService.IsAllowedAsync(user))
            return StatusCode(429, new { error = "Daily query limit reached.", tier = user.Tier });

        try
        {
            var result = await fastApi.QueryAsync(request, ct);
            cacheService.Set(request, result);
            await usageService.LogAsync(user, request.Query, request.Mode ?? "remembrancer", request.TopK ?? 6);
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in POST /api/query");
            return StatusCode(500, new { error = "FastAPI query failed.", detail = ex.Message });
        }
    }

    // ── POST /api/query/narrate ───────────────────────────────────────────────

    [HttpPost("narrate")]
    public async Task<IActionResult> Narrate([FromBody] QueryRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
            return BadRequest(new { error = "Query cannot be empty." });

        var guard = PrepareRequest(request);
        if (guard is not null) return guard;

        var user = ForgeUser!;

        if (!settingsService.IsModeAllowed(user, "narrator"))
            return StatusCode(403, new { error = "Narrator mode requires Adept tier or higher.", tier = user.Tier });

        if (!await usageService.IsAllowedAsync(user))
            return StatusCode(429, new { error = "Daily query limit reached.", tier = user.Tier });

        try
        {
            request.Mode = "narrator";
            var result = await fastApi.QueryNarrateAsync(request, ct);
            await usageService.LogAsync(user, request.Query, "narrator", request.TopK ?? 6);
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in POST /api/query/narrate");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // ── POST /api/query/explore ───────────────────────────────────────────────

    [HttpPost("explore")]
    public async Task<IActionResult> Explore([FromBody] QueryRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
            return BadRequest(new { error = "Query cannot be empty." });

        var guard = PrepareRequest(request);
        if (guard is not null) return guard;

        var user = ForgeUser!;

        if (!settingsService.IsModeAllowed(user, "explorer"))
            return StatusCode(403, new { error = "Explorer mode requires Adept tier or higher.", tier = user.Tier });

        if (!await usageService.IsAllowedAsync(user))
            return StatusCode(429, new { error = "Daily query limit reached.", tier = user.Tier });

        try
        {
            request.Mode = "explorer";
            var result = await fastApi.QueryExplorerAsync(request, ct);
            await usageService.LogAsync(user, request.Query, "explorer", request.TopK ?? 6);
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in POST /api/query/explore");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // ── POST /api/query/stream ────────────────────────────────────────────────

    [HttpPost("stream")]
    public async Task Stream([FromBody] QueryRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Query)) { Response.StatusCode = 400; return; }

        var user = ForgeUser;
        if (user is null) { Response.StatusCode = 401; return; }

        settingsService.ApplyToRequest(request, user);
        request.SessionId = sessionService.GetOrCreate(user.Id, user.Tier);

        if (!await usageService.IsAllowedAsync(user))
        {
            Response.StatusCode = 429;
            await Response.WriteAsJsonAsync(new { error = "Daily query limit reached." });
            return;
        }

        Response.Headers["Content-Type"]      = "text/event-stream";
        Response.Headers["Cache-Control"]     = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";

        try
        {
            await foreach (var token in fastApi.QueryStreamAsync(request, ct))
            {
                await Response.WriteAsync($"data: {token}\n\n", ct);
                await Response.Body.FlushAsync(ct);
            }
            await Response.WriteAsync("data: [DONE]\n\n", ct);
            await usageService.LogAsync(user, request.Query, "remembrancer", request.TopK ?? 6);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in POST /api/query/stream");
            await Response.WriteAsync($"data: [ERROR] {ex.Message}\n\n", ct);
        }
    }

    // ── POST /api/query/narrate/stream ────────────────────────────────────────

    [HttpPost("narrate/stream")]
    public async Task NarrateStream([FromBody] QueryRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Query)) { Response.StatusCode = 400; return; }

        var user = ForgeUser;
        if (user is null) { Response.StatusCode = 401; return; }

        if (!settingsService.IsModeAllowed(user, "narrator"))
        {
            Response.StatusCode = 403;
            await Response.WriteAsJsonAsync(new { error = "Narrator mode requires Adept tier." });
            return;
        }

        settingsService.ApplyToRequest(request, user);
        request.SessionId = sessionService.GetOrCreate(user.Id, user.Tier);
        request.Mode = "narrator";

        Response.Headers["Content-Type"]      = "text/event-stream";
        Response.Headers["Cache-Control"]     = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";

        try
        {
            await foreach (var token in fastApi.QueryNarrateStreamAsync(request, ct))
            {
                await Response.WriteAsync($"data: {token}\n\n", ct);
                await Response.Body.FlushAsync(ct);
            }
            await Response.WriteAsync("data: [DONE]\n\n", ct);
            await usageService.LogAsync(user, request.Query, "narrator", request.TopK ?? 6);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in POST /api/query/narrate/stream");
            await Response.WriteAsync($"data: [ERROR] {ex.Message}\n\n", ct);
        }
    }
}
