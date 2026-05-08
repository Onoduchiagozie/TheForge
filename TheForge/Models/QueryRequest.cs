namespace TheForge.Models;

// ── Requests ─────────────────────────────────────────────────────────────────

public class QueryRequest
{
    public string Query { get; set; } = string.Empty;
    public string? SessionId { get; set; }
    public string? Mode { get; set; }
    public string? BookFilter { get; set; }
    public string? SourceFilter { get; set; }
    public int? TopK { get; set; }
    public int? CandidatePool { get; set; }
    public int? StitchingWindow { get; set; }
}

// ── Response building-blocks ──────────────────────────────────────────────────

public class SourceInfo
{
    public string Source { get; set; } = string.Empty;
    public string Chapter { get; set; } = string.Empty;
    public string StitchRange { get; set; } = string.Empty;
    public double Score { get; set; }
}

public class QueryResponse
{
    public string Query { get; set; } = string.Empty;
    public string Response { get; set; } = string.Empty;
    public List<SourceInfo> Sources { get; set; } = [];
    public int ChunksUsed { get; set; }
}

// ── System payloads ───────────────────────────────────────────────────────────

public class HealthPayload
{
    public string Status { get; set; } = string.Empty;
    public string ActiveProfile { get; set; } = string.Empty;
    public string MachineRole { get; set; } = string.Empty;
    public string OllamaModel { get; set; } = string.Empty;
    public string OllamaUrl { get; set; } = string.Empty;
    public int MetadataLoaded { get; set; }
}

public class InfoPayload
{
    public int IndexVectors { get; set; }
    public int IndexDim { get; set; }
    public string MachineRole { get; set; } = string.Empty;
    public object? Manifest { get; set; }
    public object? Retrieval { get; set; }
    public int CachedSources { get; set; }
}

public class SourceListPayload
{
    public int Total { get; set; }
    public List<string> Sources { get; set; } = [];
}

public class SourceChunksPayload
{
    public string Source { get; set; } = string.Empty;
    public int Matched { get; set; }
    public List<object> Chunks { get; set; } = [];
}

public class MemoryPayload
{
    public string SessionId { get; set; } = string.Empty;
    public List<object> Memory { get; set; } = [];
}

public class Empty
{
    public string Status { get; set; } = string.Empty;
}