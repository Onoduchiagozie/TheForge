namespace TheForge.Services;

/// <summary>
/// Manages session IDs passed to FastAPI as session_id.
/// Free tier: daily session (resets at midnight — conversation clears each day).
/// Adept/Archmagos: persistent UUID until user explicitly clears it.
/// </summary>
public class SessionService
{
    private readonly Dictionary<int, string> _sessions = new();
    private readonly object _lock = new();

    public string GetOrCreate(int userId, string tier)
    {
        lock (_lock)
        {
            if (tier.Equals("free", StringComparison.OrdinalIgnoreCase))
            {
                var dailyKey = $"free-{userId}-{DateTime.UtcNow:yyyy-MM-dd}";
                if (_sessions.TryGetValue(userId, out var existing) && existing == dailyKey)
                    return existing;
                return _sessions[userId] = dailyKey;
            }

            if (!_sessions.TryGetValue(userId, out var session))
                session = _sessions[userId] = $"user-{userId}-{Guid.NewGuid():N}";

            return session;
        }
    }

    public string Rotate(int userId, string tier)
    {
        lock (_lock)
        {
            var newSession = tier.Equals("free", StringComparison.OrdinalIgnoreCase)
                ? $"free-{userId}-{DateTime.UtcNow:yyyy-MM-dd}"
                : $"user-{userId}-{Guid.NewGuid():N}";
            return _sessions[userId] = newSession;
        }
    }

    public string? Peek(int userId)
    {
        lock (_lock)
        {
            return _sessions.TryGetValue(userId, out var s) ? s : null;
        }
    }
}
