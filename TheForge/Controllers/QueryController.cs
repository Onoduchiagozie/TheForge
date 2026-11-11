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

    // Narrate — streamed via SSE.
    // Success is only emitted after a real upstream [DONE] AND answer text.
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

        var receivedAnswer = false;
        try
        {
            await foreach (var frame in fastApi.QueryNarrateStreamAsync(request, ct))
            {
                if (frame == "[DONE]")
                {
                    await WriteFrameAsync(receivedAnswer
                        ? "[DONE]"
                        : "[ERROR] Retrieval finished but the language model returned no answer.", ct);
                    return;
                }

                if (frame.StartsWith("[ERROR]", StringComparison.Ordinal))
                {
                    await WriteFrameAsync(frame, ct);
                    return;
                }

                if (frame.StartsWith("__STAGE__:failed", StringComparison.Ordinal) ||
                    frame.StartsWith("__STAGE__:error", StringComparison.Ordinal))
                {
                    await WriteFrameAsync("[ERROR] The upstream generation failed.", ct);
                    return;
                }

                if (!frame.StartsWith("__STAGE__:", StringComparison.Ordinal) &&
                    !frame.StartsWith("__SOURCES__:", StringComparison.Ordinal) &&
                    !string.IsNullOrWhiteSpace(frame))
                {
                    receivedAnswer = true;
                }

                await WriteFrameAsync(frame, ct);
            }

            // An EOF without DONE is a protocol failure, even if some text
            // and sources were already delivered.
            await WriteFrameAsync("[ERROR] The response stream closed before completion.", ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            logger.LogInformation("Narrator stream cancelled by client.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Narrator SSE gateway failed.");
            if (!ct.IsCancellationRequested)
            {
                try
                {
                    await WriteFrameAsync(
                        "[ERROR] The upstream response failed or disconnected.",
                        ct);
                }
                catch (Exception writeError) when (
                    writeError is IOException or OperationCanceledException)
                {
                    logger.LogWarning(writeError, "Client disconnected during error frame.");
                }
            }
        }
    }

    private async Task WriteFrameAsync(string payload, CancellationToken ct)
    {
        await Response.WriteAsync(SseFrameParser.EncodeData(payload), ct);
        await Response.Body.FlushAsync(ct);
    }
}
