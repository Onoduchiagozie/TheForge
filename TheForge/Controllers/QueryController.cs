using Microsoft.AspNetCore.Mvc;
using TheForge.Models;
using TheForge.Services;

namespace TheForge.Controllers;

[ApiController]
[Route("api/query")]
public class QueryController(
    IFastApiClient fastApi,
    QueryCacheService cacheService,
    ILogger<QueryController> logger) : ControllerBase
{
    // Legacy default route uses Oracle's Explorer endpoint.
    [HttpPost]
    public async Task<IActionResult> Query([FromBody] QueryRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return BadRequest(new { error = "Query cannot be empty." });
        }

        EnsureSession(request);
        var cached = cacheService.Get(request);
        if (cached is not null)
        {
            return Ok(cached);
        }

        try
        {
            var result = await fastApi.QueryAsync(request, ct);
            cacheService.Set(request, result);
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in POST /api/query");
            return StatusCode(500, new { error = "The retrieval service is unavailable." });
        }
    }

    // Explore
    [HttpPost("explore")]
    public async Task<IActionResult> Explore([FromBody] QueryRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return BadRequest(new { error = "Query cannot be empty." });
        }

        EnsureSession(request);
        try
        {
            var result = await fastApi.QueryExplorerAsync(request, ct);
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in POST /api/query/explore");
            return StatusCode(500, new { error = "The retrieval service is unavailable." });
        }
    }

    // Focus — sync, narrow, ≤max_sources
    [HttpPost("focus")]
    public async Task<IActionResult> Focus([FromBody] QueryRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return BadRequest(new { error = "Query cannot be empty." });
        }

        EnsureSession(request);
        try
        {
            var result = await fastApi.QueryFocusAsync(request, ct);
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in POST /api/query/focus");
            return StatusCode(500, new { error = "The retrieval service is unavailable." });
        }
    }

    // Narrate — streamed via SSE
    [HttpPost("narrate")]
    public async Task NarrateStream([FromBody] QueryRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        EnsureSession(request);
        Response.Headers.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";

        try
        {
            await foreach (var token in fastApi.QueryNarrateStreamAsync(request, ct))
            {
                var safeToken = token.StartsWith("[ERROR]", StringComparison.Ordinal)
                    ? "[ERROR] The retrieval service could not complete the request."
                    : token;
                await Response.WriteAsync(ServerSentEvents.Encode(safeToken), ct);
                await Response.Body.FlushAsync(ct);
                if (token.StartsWith("[ERROR]", StringComparison.Ordinal)) return;
            }

            await Response.WriteAsync("data: [DONE]\n\n", ct);
            await Response.Body.FlushAsync(ct);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Narrate stream cancelled.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in POST /api/query/narrate/stream");
            await Response.WriteAsync(ServerSentEvents.Encode("[ERROR] The retrieval service could not complete the request."), ct);
            await Response.Body.FlushAsync(ct);
        }
    }

    private static void EnsureSession(QueryRequest request)
    {
        // Legacy callers without a session must not use Oracle's shared "default" memory.
        if (string.IsNullOrWhiteSpace(request.SessionId)
            || string.Equals(request.SessionId, "default", StringComparison.OrdinalIgnoreCase))
        {
            request.SessionId = $"forge-{Guid.NewGuid():N}";
        }
    }
}
