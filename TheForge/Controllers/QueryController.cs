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

    // Narrate — streamed via SSE
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
                await Response.WriteAsync($"data: {token}\n\n", ct);
                await Response.Body.FlushAsync(ct);
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
            await Response.WriteAsync($"data: [ERROR] {ex.Message}\n\n", ct);
            await Response.Body.FlushAsync(ct);
        }
    }
}