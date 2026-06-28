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
    private static readonly string[] PublicApiPrefixes =
    [
        "/api/health",
        "/api/auth"
    ];

    public async Task InvokeAsync(HttpContext context, ForgeDbContext db)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        if (!path.StartsWith("/api", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        if (PublicApiPrefixes.Any(prefix =>
            path.Equals(prefix, StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase)))
        {
            await next(context);
            return;
        }

        var user = await ResolveUserAsync(context, db);
        if (user is null)
        {
            logger.LogWarning("Unauthorized request to {Path}", path);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Authentication required. Use X-Api-Key or Bearer token."
            });
            return;
        }

        context.Items["ForgeUser"] = user;
        await next(context);
    }

    private async Task<User?> ResolveUserAsync(HttpContext context, ForgeDbContext db)
    {
        if (context.Request.Headers.TryGetValue("X-Api-Key", out var apiKeyHeader) &&
            !string.IsNullOrWhiteSpace(apiKeyHeader))
        {
            var user = await ValidateApiKeyAsync(apiKeyHeader!, db);
            if (user is not null)
            {
                return user;
            }
        }

        if (context.Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            var authValue = authHeader.ToString();
            if (authValue.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return await ValidateJwtAsync(authValue["Bearer ".Length..].Trim(), db);
            }
        }

        return null;
    }

    private static async Task<User?> ValidateApiKeyAsync(string rawKey, ForgeDbContext db)
    {
        var hash = ApiKeyMiddleware.HashKey(rawKey);

        var apiKey = await db.ApiKeys
            .Include(key => key.User)
            .ThenInclude(user => user.Settings)
            .FirstOrDefaultAsync(key => key.KeyHash == hash && key.IsActive);

        return apiKey?.User.IsActive == true ? apiKey.User : null;
    }

    private async Task<User?> ValidateJwtAsync(string token, ForgeDbContext db)
    {
        var secret = config["Supabase:JwtSecret"];
        if (string.IsNullOrWhiteSpace(secret))
        {
            logger.LogWarning("JWT validation skipped because Supabase:JwtSecret is missing.");
            return null;
        }

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            };

            var principal = handler.ValidateToken(token, validationParameters, out _);
            var email =
                principal.FindFirstValue(ClaimTypes.Email) ??
                principal.FindFirstValue("email");

            if (string.IsNullOrWhiteSpace(email))
            {
                return null;
            }

            var user = await db.Users
                .Include(existingUser => existingUser.Settings)
                .FirstOrDefaultAsync(existingUser => existingUser.Email == email);

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
                    .Include(existingUser => existingUser.Settings)
                    .FirstAsync(existingUser => existingUser.Id == user.Id);
            }

            return user.IsActive ? user : null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "JWT validation failed.");
            return null;
        }
    }
}
