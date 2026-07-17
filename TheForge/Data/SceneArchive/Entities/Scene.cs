namespace TheForge.Data.SceneArchive.Entities;

/// <summary>
/// Maps to the real "scenes" table in battle_scenes.db. Only one score field and one rank
/// field exist (not the three-tier Battle/Overall/Narrative split originally assumed) —
/// matches exactly what dump_schema.py reported. WovenAt is kept as a raw string rather than
/// DateTime: the column is TEXT with no guaranteed format, and a failed parse would crash the
/// whole row read. Safer to pass it through as-is and let the UI display it directly.
/// </summary>
public class Scene
{
    public int SceneId { get; set; }

    public string BookId { get; set; } = string.Empty;
    public Book Book { get; set; } = null!;

    public string? SceneName { get; set; }
    public string? Teaser { get; set; }
    public string? Chronicle { get; set; }
    public string? QueryPrompt { get; set; }
    public string? StitchedText { get; set; }
    public string? SceneType { get; set; }
    public double Score { get; set; }
    public int Rank { get; set; }
    public string? SceneKey { get; set; }
    public string? WovenAt { get; set; }
}
