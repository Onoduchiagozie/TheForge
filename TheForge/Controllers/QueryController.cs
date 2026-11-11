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
    // Inspect (Remembrancer / default)
    [HttpPost]
    public async Task<IActionResult> Query([FromBody] QueryRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return BadRequest(new { error = "Query cannot be empty." });
        }

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
            return StatusCode(500, new { error = "FastAPI query failed.", detail = ex.Message });
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

        try
        {
            var result = await fastApi.QueryExplorerAsync(request, ct);
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in POST /api/query/explore");
            return StatusCode(500, new { error = ex.Message });
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

        try
        {
            var result = await fastApi.QueryFocusAsync(request, ct);
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in POST /api/query/focus");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // Narrate — transparent SSE proxy. Do not invent a [DONE] frame:
    // the upstream must explicitly confirm completion.
    [HttpPost("narrate")]
    public async Task NarrateStream([FromBody] QueryRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        Response.Headers.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";

        try
        {
            await foreach (var token in fastApi.QueryNarrateStreamAsync(request, ct))
            {
                // SSE data lines must ALL have a data: prefix. Otherwise
                // paragraph breaks from the model disappear downstream.
                foreach (var line in token.Split('\n'))
                {
                    await Response.WriteAsync($"data: {line.TrimEnd('\r')}\n", ct);
                }
                await Response.WriteAsync("\n", ct);
                await Response.Body.FlushAsync(ct);

                if (token == "[DONE]" ||
                    token.StartsWith("[ERROR]", StringComparison.Ordinal))
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Narrate stream cancelled.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in POST /api/query/narrate");
            if (!ct.IsCancellationRequested)
            {
                try
                {
                    await Response.WriteAsync(
                        "data: [ERROR] The gateway lost its Oracle connection.\n\n", ct);
                    await Response.Body.FlushAsync(ct);
                }
                catch (Exception sendEx)
                {
                    logger.LogWarning(sendEx, "Cannot send SSE error after disconnect.");
                }
            }
        }
    }

}