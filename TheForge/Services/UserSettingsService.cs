using Microsoft.EntityFrameworkCore;
using TheForge.Data;
using TheForge.Data.Entities;
using TheForge.Models;

namespace TheForge.Services;

/// <summary>
/// GOLD scaffold for saved settings and future tier caps.
/// Pre-Gold query requests intentionally do not apply top_k, pool, window, or mode overrides.
/// </summary>
public class UserSettingsService(ForgeDbContext db, IConfiguration config)
{
    // System defaults from appsettings.json → "QueryDefaults" section
    private int DefaultTopK => config.GetValue("QueryDefaults:TopK", 6);
    private int DefaultPool => config.GetValue("QueryDefaults:CandidatePool", 20);
    private int DefaultWindow => config.GetValue("QueryDefaults:StitchingWindow", 5);

    // Tier caps
    private static readonly Dictionary<string, (int MaxTopK, int MaxPool, string[] AllowedModes)> _tierCaps = new()
    {
        ["free"]      = (4,  10, ["remembrancer"]),
        ["adept"]     = (8,  30, ["remembrancer", "narrator", "explorer"]),
        ["archmagos"] = (10, 50, ["remembrancer", "narrator", "explorer"]),
    };

    /// <summary>
    /// Pre-Gold no-op. Gold will merge user settings + tier caps into outgoing QueryRequest.
    /// </summary>
    public void ApplyToRequest(QueryRequest req, User user)
    {
        _ = req;
        _ = user;

        // GOLD: Retrieval override and mode enforcement are intentionally disabled.
        // var settings = user.Settings;
        // var tier = user.Tier.ToLowerInvariant();
        // var caps = _tierCaps.GetValueOrDefault(tier, _tierCaps["free"]);
        //
        // req.TopK = Math.Min(
        //     settings?.TopK ?? req.TopK ?? DefaultTopK,
        //     caps.MaxTopK);
        //
        // req.CandidatePool = Math.Min(
        //     settings?.CandidatePool ?? req.CandidatePool ?? DefaultPool,
        //     caps.MaxPool);
        //
        // req.StitchingWindow ??= settings?.StitchingWindow ?? DefaultWindow;
        //
        // var requestedMode = req.Mode ?? settings?.PreferredMode ?? "remembrancer";
        // req.Mode = caps.AllowedModes.Contains(requestedMode)
        //     ? requestedMode
        //     : "remembrancer";
    }

    /// <summary>
    /// Returns true if the user is allowed to use the requested mode.
    /// </summary>
    public bool IsModeAllowed(User user, string mode)
    {
        // GOLD: Mode tier gates are intentionally disabled in the pre-Gold build.
        _ = user;
        _ = mode;
        return true;

        /*
        var tier = user.Tier.ToLowerInvariant();
        var caps = _tierCaps.GetValueOrDefault(tier, _tierCaps["free"]);
        return caps.AllowedModes.Contains(mode.ToLowerInvariant());
        */
    }

    /// <summary>
    /// Save or update user settings. Creates a new record if none exists.
    /// </summary>
    public async Task SaveAsync(int userId, UserSettingsUpdateDto dto)
    {
        var existing = await db.UserSettings.FirstOrDefaultAsync(s => s.UserId == userId);
        if (existing is null)
        {
            db.UserSettings.Add(new UserSettings
            {
                UserId = userId,
                TopK = dto.TopK,
                CandidatePool = dto.CandidatePool,
                StitchingWindow = dto.StitchingWindow,
                PreferredMode = dto.PreferredMode ?? "remembrancer",
                Temperature = dto.Temperature,
                UpdatedAt = DateTime.UtcNow,
            });
        }
        else
        {
            if (dto.TopK.HasValue) existing.TopK = dto.TopK;
            if (dto.CandidatePool.HasValue) existing.CandidatePool = dto.CandidatePool;
            if (dto.StitchingWindow.HasValue) existing.StitchingWindow = dto.StitchingWindow;
            if (dto.PreferredMode is not null) existing.PreferredMode = dto.PreferredMode;
            if (dto.Temperature.HasValue) existing.Temperature = dto.Temperature;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync();
    }
}

// ── DTO ───────────────────────────────────────────────────────────────────────

public class UserSettingsUpdateDto
{
    public int? TopK { get; set; }
    public int? CandidatePool { get; set; }
    public int? StitchingWindow { get; set; }
    public string? PreferredMode { get; set; }
    public double? Temperature { get; set; }
}
