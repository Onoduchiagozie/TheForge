using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TheForge.Data;
using TheForge.Data.Entities;

namespace TheForge.Services;

/// <summary>
/// Tracks query usage per user and enforces daily limits per tier.
/// </summary>
public class UsageService(ForgeDbContext db)
{
    private static readonly Dictionary<string, int> _dailyLimits = new()
    {
        ["free"]      = 10,
        ["adept"]     = 100,
        ["archmagos"] = int.MaxValue,   // unlimited
    };

    /// <summary>
    /// Returns true if the user has NOT exceeded their daily limit.
    /// </summary>
    public async Task<bool> IsAllowedAsync(User user)
    {
        var tier = user.Tier.ToLowerInvariant();
        var limit = _dailyLimits.GetValueOrDefault(tier, 10);
        if (limit == int.MaxValue) return true;

        var today = DateTime.UtcNow.Date;
        var count = await db.UsageLogs
            .CountAsync(l => l.UserId == user.Id && l.Timestamp.Date == today);

        return count < limit;
    }

    /// <summary>
    /// Returns remaining queries for today.
    /// </summary>
    public async Task<int> RemainingTodayAsync(User user)
    {
        var tier = user.Tier.ToLowerInvariant();
        var limit = _dailyLimits.GetValueOrDefault(tier, 10);
        if (limit == int.MaxValue) return 9999;

        var today = DateTime.UtcNow.Date;
        var count = await db.UsageLogs
            .CountAsync(l => l.UserId == user.Id && l.Timestamp.Date == today);

        return Math.Max(0, limit - count);
    }

    /// <summary>
    /// Log a query. Call this AFTER a successful FastAPI response.
    /// </summary>
    public async Task LogAsync(User user, string query, string mode, int topK)
    {
        db.UsageLogs.Add(new UsageLog
        {
            UserId = user.Id,
            QueryHash = HashQuery(query),
            Mode = mode,
            TopK = topK,
            Timestamp = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    private static string HashQuery(string query)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(query.Trim().ToLowerInvariant()));
        return Convert.ToHexString(bytes)[..16].ToLowerInvariant(); // first 16 chars is enough
    }
}
