using TheForge.Data.SceneArchive.Entities;

namespace TheForge.Services.SceneArchive;

public enum ScrollDirection
{
    Left,
    Right,
}

/// <summary>
/// One horizontal ticker row for the archive landing page: a category key, a display label,
/// a scroll direction, and a priority-ranked, rotation-shuffled list of scenes.
///
/// Still entity-level (List&lt;Scene&gt;) on purpose — SceneArchiveService (Phase 3) maps these
/// into UI-facing DTOs so the Razor layer never touches EF entities directly.
/// </summary>
public record RowPlan(string Key, string DisplayLabel, ScrollDirection Direction, List<Scene> Scenes);
