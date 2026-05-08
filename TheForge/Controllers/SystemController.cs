using Microsoft.AspNetCore.Mvc;
 using TheForge.Servcies;

namespace TheForge.Controllers;

/// <summary>
/// Proxies the FastAPI /health endpoint.
/// Use this to confirm the Python RAG service is alive before making query calls.
/// Proxies → GET /health on FastAPI.
/// </summary>
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

/// <summary>
/// Proxies the FastAPI /info endpoint.
/// Use this to inspect FAISS index statistics, active profile, and retrieval settings.
/// Proxies → GET /info on FastAPI.
/// </summary>
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

/// <summary>
/// Proxies the FastAPI /sources and /sources/{sourceName} endpoints.
/// Use the list endpoint to browse available lore sources; use the detail endpoint
/// to inspect chunks within a specific source for archive exploration.
/// Proxies → GET /sources and GET /sources/{source_name} on FastAPI.
/// </summary>
[ApiController]
[Route("api/sources")]
public class SourcesController(IFastApiClient fastApi, ILogger<SourcesController> logger) : ControllerBase
{
    /// <summary>GET /api/sources — list all source names.</summary>
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

    /// <summary>GET /api/sources/{sourceName}?limit=20 — chunks for one source.</summary>
    [HttpGet("{sourceName}")]
    public async Task<IActionResult> GetSourceChunks(
        string sourceName,
        [FromQuery] int limit = 20,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sourceName))
            return BadRequest(new { error = "sourceName is required." });

        try
        {
            var result = await fastApi.GetSourceChunksAsync(sourceName, limit, ct);
            if (result.Matched == 0)
                return NotFound(new { error = $"No chunks found for source '{sourceName}'." });
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in GET /api/sources/{SourceName}", sourceName);
            return StatusCode(500, new { error = "Failed to retrieve source chunks.", detail = ex.Message });
        }
    }
}

/// <summary>
/// Proxies the FastAPI /memory endpoints.
/// Use the GET to read a session's conversation history and DELETE to wipe it.
/// Proxies → GET /memory and DELETE /memory on FastAPI.
/// </summary>
[ApiController]
[Route("api/memory")]
public class SessionsController(IFastApiClient fastApi, ILogger<SessionsController> logger) : ControllerBase
{
    /// <summary>GET /api/memory?session_id=default — read session memory.</summary>
    [HttpGet]
    public async Task<IActionResult> GetMemory([FromQuery] string session_id = "default", CancellationToken ct = default)
    {
        try
        {
            var result = await fastApi.GetMemoryAsync(session_id, ct);
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in GET /api/memory for session {SessionId}", session_id);
            return StatusCode(500, new { error = "Failed to retrieve session memory.", detail = ex.Message });
        }
    }

    /// <summary>DELETE /api/memory?session_id=default — clear session memory.</summary>
    [HttpDelete]
    public async Task<IActionResult> ClearMemory([FromQuery] string session_id = "default", CancellationToken ct = default)
    {
        try
        {
            var result = await fastApi.ClearMemoryAsync(session_id, ct);
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in DELETE /api/memory for session {SessionId}", session_id);
            return StatusCode(500, new { error = "Failed to clear session memory.", detail = ex.Message });
        }
    }
}