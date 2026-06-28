using Microsoft.AspNetCore.Mvc;
using TheForge.Services;

namespace TheForge.Controllers;

[ApiController]
[Route("api/health")]
public class HealthController(IFastApiClient fastApi, ILogger<HealthController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Health(CancellationToken ct)
    {
        try
        {
            var result = await fastApi.HealthAsync(ct);
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in GET /api/health");
            return StatusCode(500, new { error = "FastAPI health check failed.", detail = ex.Message });
        }
    }
}

[ApiController]
[Route("api/info")]
public class InfoController(IFastApiClient fastApi, ILogger<InfoController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Info(CancellationToken ct)
    {
        try
        {
            var result = await fastApi.InfoAsync(ct);
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in GET /api/info");
            return StatusCode(500, new { error = "FastAPI info call failed.", detail = ex.Message });
        }
    }
}

[ApiController]
[Route("api/sources")]
public class SourcesController(IFastApiClient fastApi, ILogger<SourcesController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> ListSources(CancellationToken ct)
    {
        try
        {
            var result = await fastApi.ListSourcesAsync(ct);
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in GET /api/sources");
            return StatusCode(500, new { error = "Failed to retrieve source list.", detail = ex.Message });
        }
    }

    [HttpGet("{sourceName}")]
    public async Task<IActionResult> GetSourceChunks(string sourceName, [FromQuery] int limit = 20, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sourceName))
        {
            return BadRequest(new { error = "sourceName is required." });
        }

        try
        {
            var result = await fastApi.GetSourceChunksAsync(sourceName, limit, ct);
            if (result.Matched == 0)
            {
                return NotFound(new { error = $"No chunks found for source '{sourceName}'." });
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in GET /api/sources/{SourceName}", sourceName);
            return StatusCode(500, new { error = "Failed to retrieve source chunks.", detail = ex.Message });
        }
    }
}

// GOLD: Context/session memory endpoints are intentionally disabled in the pre-Gold build.
// [ApiController]
// [Route("api/memory")]
// public class SessionsController(IFastApiClient fastApi, ILogger<SessionsController> logger) : ControllerBase
// {
//     [HttpGet]
//     public async Task<IActionResult> GetMemory([FromQuery] string session_id = "default", CancellationToken ct = default)
//     {
//         try
//         {
//             var result = await fastApi.GetMemoryAsync(session_id, ct);
//             return Ok(result);
//         }
//         catch (Exception ex)
//         {
//             logger.LogError(ex, "Error in GET /api/memory for session {SessionId}", session_id);
//             return StatusCode(500, new { error = "Failed to retrieve session memory.", detail = ex.Message });
//         }
//     }
//
//     [HttpDelete]
//     public async Task<IActionResult> ClearMemory([FromQuery] string session_id = "default", CancellationToken ct = default)
//     {
//         try
//         {
//             var result = await fastApi.ClearMemoryAsync(session_id, ct);
//             return Ok(result);
//         }
//         catch (Exception ex)
//         {
//             logger.LogError(ex, "Error in DELETE /api/memory for session {SessionId}", session_id);
//             return StatusCode(500, new { error = "Failed to clear session memory.", detail = ex.Message });
//         }
//     }
// }
