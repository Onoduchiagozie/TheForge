namespace TheForge.Data.Entities;

// ── Users ─────────────────────────────────────────────────────────────────────

public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Tier { get; set; } = "free";           // free | adept | archmagos
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    // Navigation
    public UserSettings? Settings { get; set; }
    public List<ApiKey> ApiKeys { get; set; } = [];
    public List<UsageLog> UsageLogs { get; set; } = [];
}

// ── UserSettings ──────────────────────────────────────────────────────────────

public class UserSettings
{
    public int Id { get; set; }
    public int UserId { get; set; }

    // Retrieval overrides — null means use system default from appsettings
    public int? TopK { get; set; }
    public int? CandidatePool { get; set; }
    public int? StitchingWindow { get; set; }

    // Mode preference: remembrancer | narrator | explorer
    public string PreferredMode { get; set; } = "remembrancer";

    // Generation overrides
    public double? Temperature { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public User User { get; set; } = null!;
}

// ── ApiKeys ───────────────────────────────────────────────────────────────────

public class ApiKey
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string KeyHash { get; set; } = string.Empty;   // store hash, never raw key
    public string Name { get; set; } = string.Empty;      // e.g. "Blazor frontend"
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    // Navigation
    public User User { get; set; } = null!;
}

// ── UsageLog ──────────────────────────────────────────────────────────────────

public class UsageLog
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string QueryHash { get; set; } = string.Empty; // SHA256 of query text
    public string Mode { get; set; } = string.Empty;
    public int TopK { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    // Navigation
    public User User { get; set; } = null!;
}
