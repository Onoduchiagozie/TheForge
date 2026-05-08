using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TheForge.Data;
using TheForge.Data.Entities;

namespace TheForge.Middleware;

public class ForgeAuthMiddleware(
    RequestDelegate next,
    IConfiguration config,
    ILogger<ForgeAuthMiddleware> logger)
{
    private static readonly string[] _publicPrefixes =
    [
        "/swagger",
        "/api/health",
        "/api/auth"
    ];

    public async Task InvokeAsync(HttpContext context, ForgeDbContext db)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        // ✅ Correct public route matching
        if (_publicPrefixes.Any(p =>
            path.Equals(p, StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith(p + "/", StringComparison.OrdinalIgnoreCase)))
        {
            await next(context);
            return;
        }

        logger.LogInformation("Auth middleware hit: {Path}", path);

        User? user = null;

        // ── API KEY ─────────────────────────────────────────────
        if (context.Request.Headers.TryGetValue("X-Api-Key", out var apiKeyHeader) &&
            !string.IsNullOrWhiteSpace(apiKeyHeader))
        {
            user = await ValidateApiKey(apiKeyHeader!, db);
        }

        // ── JWT ────────────────────────────────────────────────
        if (user is null &&
            context.Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            var auth = authHeader.ToString();

            if (auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                var token = auth["Bearer ".Length..].Trim();
                user = await ValidateJwt(token, db);
            }
        }

        if (user is null)
        {
            logger.LogWarning("Unauthorized request → {Path}", path);

            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Authentication required. Use X-Api-Key or Bearer token."
            });
            return;
        }

        context.Items["ForgeUser"] = user;

        logger.LogInformation("Authenticated user: {Email}", user.Email);

        await next(context);
    }

    // ── API KEY VALIDATION ─────────────────────────────────────

    private static async Task<User?> ValidateApiKey(string rawKey, ForgeDbContext db)
    {
        var hash = ApiKeyMiddleware.HashKey(rawKey);

        var key = await db.ApiKeys
            .Include(k => k.User)
            .ThenInclude(u => u.Settings)
            .FirstOrDefaultAsync(k => k.KeyHash == hash && k.IsActive);

        return key?.User.IsActive == true ? key.User : null;
    }

    // ── JWT VALIDATION ─────────────────────────────────────────

    private async Task<User?> ValidateJwt(string token, ForgeDbContext db)
    {
        var secret = config["Supabase:JwtSecret"];
        if (string.IsNullOrWhiteSpace(secret))
        {
            logger.LogWarning("JWT secret missing");
            return null;
        }

        try
        {
            var handler = new JwtSecurityTokenHandler();

            var validationParams = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            };

            var principal = handler.ValidateToken(token, validationParams, out _);

            var email =
                principal.FindFirstValue(ClaimTypes.Email) ??
                principal.FindFirstValue("email");

            if (string.IsNullOrWhiteSpace(email))
                return null;

            var user = await db.Users
                .Include(u => u.Settings)
                .FirstOrDefaultAsync(u => u.Email == email);

            // ── AUTO-PROVISION ───────────────────────────────
            if (user is null)
            {
                user = new User
                {
                    Email = email,
                    PasswordHash = "supabase",
                    Tier = "free",
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                };

                db.Users.Add(user);
                await db.SaveChangesAsync();

                db.UserSettings.Add(new UserSettings
                {
                    UserId = user.Id,
                    PreferredMode = "remembrancer",
                    UpdatedAt = DateTime.UtcNow
                });

                await db.SaveChangesAsync();

                user = await db.Users
                    .Include(u => u.Settings)
                    .FirstAsync(u => u.Id == user.Id);
            }

            return user.IsActive ? user : null;
        }
        catch (Exception ex)
        {
            logger.LogWarning("JWT validation failed: {Message}", ex.Message);
            return null;
        }
    }
}