using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TheForge.Data;
using TheForge.Data.Entities;
using TheForge.Middleware;

namespace TheForge.Controllers;

/// <summary>
/// Public auth endpoints — no X-Api-Key required.
/// Handles user registration, login, and API key generation.
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController(ForgeDbContext db, ILogger<AuthController> logger) : ControllerBase
{
    // ── POST /api/auth/register ───────────────────────────────────────────────

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
            return BadRequest(new { error = "Email and password are required." });

        var exists = await db.Users.AnyAsync(u => u.Email == dto.Email);
        if (exists)
            return Conflict(new { error = "An account with that email already exists." });

        var user = new User
        {
            Email = dto.Email.Trim().ToLowerInvariant(),
            PasswordHash = HashPassword(dto.Password),
            Tier = "free",
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        // Create default settings
        db.UserSettings.Add(new UserSettings
        {
            UserId = user.Id,
            PreferredMode = "remembrancer",
            UpdatedAt = DateTime.UtcNow,
        });

        // Generate first API key automatically
        var rawKey = GenerateKey();
        db.ApiKeys.Add(new ApiKey
        {
            UserId = user.Id,
            KeyHash = ApiKeyMiddleware.HashKey(rawKey),
            Name = "Default key",
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });

        await db.SaveChangesAsync();

        logger.LogInformation("New user registered: {Email}", user.Email);

        return Ok(new
        {
            message = "Account created.",
            email = user.Email,
            tier = user.Tier,
            // Return raw key ONCE — we never store this, only its hash
            api_key = rawKey,
            warning = "Save this API key — it will not be shown again.",
        });
    }

    // ── POST /api/auth/login ──────────────────────────────────────────────────

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
            return BadRequest(new { error = "Email and password are required." });

        var user = await db.Users
            .Include(u => u.ApiKeys)
            .FirstOrDefaultAsync(u => u.Email == dto.Email.Trim().ToLowerInvariant());

        if (user is null || !VerifyPassword(dto.Password, user.PasswordHash))
            return Unauthorized(new { error = "Invalid email or password." });

        if (!user.IsActive)
            return Unauthorized(new { error = "Account is disabled." });

        // Return the user's existing active keys (hashed — can't show raw)
        // If they lost their key, they need to generate a new one
        var keyCount = user.ApiKeys.Count(k => k.IsActive);

        return Ok(new
        {
            message = "Login confirmed.",
            email = user.Email,
            tier = user.Tier,
            api_key = keyCount,
            hint = "If you lost your API key, call POST /api/auth/new-key to generate a replacement.",
        });
    }

    // ── POST /api/auth/new-key ────────────────────────────────────────────────
    // Requires email + password in body — not X-Api-Key (since they may have lost it)

    [HttpPost("new-key")]
    public async Task<IActionResult> NewKey([FromBody] LoginDto dto)
    {
        var user = await db.Users
            .Include(u => u.ApiKeys)
            .FirstOrDefaultAsync(u => u.Email == dto.Email.Trim().ToLowerInvariant());

        if (user is null || !VerifyPassword(dto.Password, user.PasswordHash))
            return Unauthorized(new { error = "Invalid email or password." });

        // Deactivate all old keys
        foreach (var key in user.ApiKeys.Where(k => k.IsActive))
            key.IsActive = false;

        // Generate new key
        var rawKey = GenerateKey();
        db.ApiKeys.Add(new ApiKey
        {
            UserId = user.Id,
            KeyHash = ApiKeyMiddleware.HashKey(rawKey),
            Name = $"Key generated {DateTime.UtcNow:yyyy-MM-dd}",
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });

        await db.SaveChangesAsync();
        logger.LogInformation("New API key generated for user {Email}", user.Email);

        return Ok(new
        {
            message = "New API key generated. Old keys deactivated.",
            api_key = rawKey,
            warning = "Save this API key — it will not be shown again.",
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string HashPassword(string password)
    {
        // Simple SHA256 hash for v1 — replace with BCrypt in production
        var bytes = SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(password + "forge-salt-2026"));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static bool VerifyPassword(string password, string storedHash)
        => HashPassword(password) == storedHash;

    private static string GenerateKey()
    {
        // forge- prefix makes keys recognisable, 32 random bytes = 256-bit entropy
        var bytes = RandomNumberGenerator.GetBytes(32);
        return $"forge-{Convert.ToBase64String(bytes).Replace("+", "").Replace("/", "").Replace("=", "")[..32]}";
    }
}

// ── DTOs ──────────────────────────────────────────────────────────────────────

public record RegisterDto(string Email, string Password);
public record LoginDto(string Email, string Password);
// using System.Security.Cryptography;
// using Microsoft.AspNetCore.Mvc;
// using Microsoft.EntityFrameworkCore;
// using TheForge.Data;
// using TheForge.Data.Entities;
// using TheForge.Middleware;
//
// namespace TheForge.Controllers;
//
// /// <summary>
// /// Public auth endpoints — no X-Api-Key required.
// /// Handles user registration, login, and API key generation.
// /// </summary>
// [ApiController]
// [Route("api/auth")]
// public class AuthController(ForgeDbContext db, ILogger<AuthController> logger) : ControllerBase
// {
//     // ── POST /api/auth/register ───────────────────────────────────────────────
//
//     [HttpPost("register")]
//     public async Task<IActionResult> Register([FromBody] RegisterDto dto)
//     {
//         if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
//             return BadRequest(new { error = "Email and password are required." });
//
//         var exists = await db.Users.AnyAsync(u => u.Email == dto.Email);
//         if (exists)
//             return Conflict(new { error = "An account with that email already exists." });
//
//         var user = new User
//         {
//             Email = dto.Email.Trim().ToLowerInvariant(),
//             PasswordHash = HashPassword(dto.Password),
//             Tier = "free",
//             CreatedAt = DateTime.UtcNow,
//             IsActive = true,
//         };
//         db.Users.Add(user);
//         await db.SaveChangesAsync();
//
//         // Create default settings
//         db.UserSettings.Add(new UserSettings
//         {
//             UserId = user.Id,
//             PreferredMode = "remembrancer",
//             UpdatedAt = DateTime.UtcNow,
//         });
//
//         // Generate first API key automatically
//         var rawKey = GenerateKey();
//         db.ApiKeys.Add(new ApiKey
//         {
//             UserId = user.Id,
//             KeyHash = ApiKeyMiddleware.HashKey(rawKey),
//             Name = "Default key",
//             CreatedAt = DateTime.UtcNow,
//             IsActive = true,
//         });
//
//         await db.SaveChangesAsync();
//
//         logger.LogInformation("New user registered: {Email}", user.Email);
//
//         return Ok(new
//         {
//             message = "Account created.",
//             email = user.Email,
//             tier = user.Tier,
//             // Return raw key ONCE — we never store this, only its hash
//             api_key = rawKey,
//             warning = "Save this API key — it will not be shown again.",
//         });
//     }
//
//     // ── POST /api/auth/login ──────────────────────────────────────────────────
//
//     [HttpPost("login")]
//     public async Task<IActionResult> Login([FromBody] LoginDto dto)
//     {
//         if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
//             return BadRequest(new { error = "Email and password are required." });
//
//         var user = await db.Users
//             .Include(u => u.ApiKeys)
//             .FirstOrDefaultAsync(u => u.Email == dto.Email.Trim().ToLowerInvariant());
//
//         if (user is null || !VerifyPassword(dto.Password, user.PasswordHash))
//             return Unauthorized(new { error = "Invalid email or password." });
//
//         if (!user.IsActive)
//             return Unauthorized(new { error = "Account is disabled." });
//
//         // Return the user's existing active keys (hashed — can't show raw)
//         // If they lost their key, they need to generate a new one
//         var keyCount = user.ApiKeys.Count(k => k.IsActive);
//
//         return Ok(new
//         {
//             message = "Login confirmed.",
//             email = user.Email,
//             tier = user.Tier,
//             active_keys = keyCount,
//             hint = "If you lost your API key, call POST /api/auth/new-key to generate a replacement.",
//         });
//     }
//
//     // ── POST /api/auth/new-key ────────────────────────────────────────────────
//     // Requires email + password in body — not X-Api-Key (since they may have lost it)
//
//     [HttpPost("new-key")]
//     public async Task<IActionResult> NewKey([FromBody] LoginDto dto)
//     {
//         var user = await db.Users
//             .Include(u => u.ApiKeys)
//             .FirstOrDefaultAsync(u => u.Email == dto.Email.Trim().ToLowerInvariant());
//
//         if (user is null || !VerifyPassword(dto.Password, user.PasswordHash))
//             return Unauthorized(new { error = "Invalid email or password." });
//
//         // Deactivate all old keys
//         foreach (var key in user.ApiKeys.Where(k => k.IsActive))
//             key.IsActive = false;
//
//         // Generate new key
//         var rawKey = GenerateKey();
//         db.ApiKeys.Add(new ApiKey
//         {
//             UserId = user.Id,
//             KeyHash = ApiKeyMiddleware.HashKey(rawKey),
//             Name = $"Key generated {DateTime.UtcNow:yyyy-MM-dd}",
//             CreatedAt = DateTime.UtcNow,
//             IsActive = true,
//         });
//
//         await db.SaveChangesAsync();
//         logger.LogInformation("New API key generated for user {Email}", user.Email);
//
//         return Ok(new
//         {
//             message = "New API key generated. Old keys deactivated.",
//             api_key = rawKey,
//             warning = "Save this API key — it will not be shown again.",
//         });
//     }
//
//     // ── Helpers ───────────────────────────────────────────────────────────────
//
//     private static string HashPassword(string password)
//     {
//         // Simple SHA256 hash for v1 — replace with BCrypt in production
//         var bytes = SHA256.HashData(
//             System.Text.Encoding.UTF8.GetBytes(password + "forge-salt-2026"));
//         return Convert.ToHexString(bytes).ToLowerInvariant();
//     }
//
//     private static bool VerifyPassword(string password, string storedHash)
//         => HashPassword(password) == storedHash;
//
//     private static string GenerateKey()
//     {
//         // forge- prefix makes keys recognisable, 32 random bytes = 256-bit entropy
//         var bytes = RandomNumberGenerator.GetBytes(32);
//         return $"forge-{Convert.ToBase64String(bytes).Replace("+", "").Replace("/", "").Replace("=", "")[..32]}";
//     }
// }
//
// // ── DTOs ──────────────────────────────────────────────────────────────────────
//
// public record RegisterDto(string Email, string Password);
// public record LoginDto(string Email, string Password);