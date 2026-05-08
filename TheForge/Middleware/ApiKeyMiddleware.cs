using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TheForge.Data;

namespace TheForge.Middleware;

/// <summary>
/// Checks X-Api-Key header on every request.
/// Looks up the SHA-256 hash of the provided key in the ApiKeys table.
/// If valid, attaches the matching User to HttpContext.Items["ForgeUser"]
/// so downstream middleware and controllers can read tier and settings.
/// Skips auth for: /swagger, /health (unauthenticated ping).
/// </summary>
public class ApiKeyMiddleware(RequestDelegate next, ILogger<ApiKeyMiddleware> logger)
{
    private static readonly HashSet<string> _skipPaths =
    [
        "/swagger",
        "/api/health",
        "/"
    ];

    public async Task InvokeAsync(HttpContext context, ForgeDbContext db)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        // Skip auth for public paths
        if (_skipPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            await next(context);
            return;
        }

        // Read the API key from header
        if (!context.Request.Headers.TryGetValue("X-Api-Key", out var keyValue) ||
            string.IsNullOrWhiteSpace(keyValue))
        {
            logger.LogWarning("Request to {Path} rejected — missing X-Api-Key header", path);
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Missing API key. Add X-Api-Key header."
            });
            return;
        }

        // Hash the provided key and look it up
        var keyHash = HashKey(keyValue!);
        var apiKey = await db.ApiKeys
            .Include(k => k.User)
            .ThenInclude(u => u.Settings)
            .FirstOrDefaultAsync(k => k.KeyHash == keyHash && k.IsActive);

        if (apiKey is null || !apiKey.User.IsActive)
        {
            logger.LogWarning("Request to {Path} rejected — invalid API key", path);
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "Invalid or inactive API key." });
            return;
        }

        // Attach user to context for downstream use
        context.Items["ForgeUser"] = apiKey.User;
        await next(context);
    }

    public static string HashKey(string rawKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawKey));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
