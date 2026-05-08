using Microsoft.AspNetCore.Mvc;
using TheForge.Data.Entities;
using TheForge.Services;

namespace TheForge.Controllers;

[ApiController]
[Route("api/settings")]
public class SettingsController(
    UserSettingsService settingsService,
    ILogger<SettingsController> logger) : ControllerBase
{
    private User? ForgeUser => HttpContext.Items["ForgeUser"] as User;

    /// <summary>
    /// GET /api/settings — returns current user's settings and tier info.
    /// </summary>
    [HttpGet]
    public IActionResult Get()
    {
        var user = ForgeUser;
        if (user is null) return Unauthorized();

        var settings = user.Settings;
        return Ok(new
        {
            tier = user.Tier,
            email = user.Email,
            preferred_mode = settings?.PreferredMode ?? "remembrancer",
            top_k = settings?.TopK,
            candidate_pool = settings?.CandidatePool,
            stitching_window = settings?.StitchingWindow,
            temperature = settings?.Temperature,
            updated_at = settings?.UpdatedAt,
        });
    }

    /// <summary>
    /// PUT /api/settings — update current user's settings.
    /// Tier caps are enforced server-side — you cannot set top_k higher than your tier allows.
    /// </summary>
    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UserSettingsUpdateDto dto)
    {
        var user = ForgeUser;
        if (user is null) return Unauthorized();

        try
        {
            await settingsService.SaveAsync(user.Id, dto);
            logger.LogInformation("Settings updated for user {UserId}", user.Id);
            return Ok(new { message = "Settings updated." });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating settings for user {UserId}", user.Id);
            return StatusCode(500, new { error = "Failed to update settings." });
        }
    }

    /// <summary>
    /// GET /api/settings/tier — returns tier limits for the current user.
    /// Useful for the Blazor UI to know what to show/hide.
    /// </summary>
    [HttpGet("tier")]
    public IActionResult TierInfo()
    {
        var user = ForgeUser;
        if (user is null) return Unauthorized();

        var tierLimits = user.Tier.ToLowerInvariant() switch
        {
            "adept"     => new { tier = "adept",     daily_limit = 100, max_top_k = 8,  modes = new[] { "remembrancer", "narrator", "explorer" } },
            "archmagos" => new { tier = "archmagos", daily_limit = -1,  max_top_k = 10, modes = new[] { "remembrancer", "narrator", "explorer" } },
            _           => new { tier = "free",      daily_limit = 10,  max_top_k = 4,  modes = new[] { "remembrancer" } },
        };

        return Ok(tierLimits);
    }
}
