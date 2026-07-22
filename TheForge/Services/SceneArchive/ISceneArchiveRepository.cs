using TheForge.Data.SceneArchive.Entities;

namespace TheForge.Services.SceneArchive;

/// <summary>
/// Raw, paged data access over battlescenes.db. Deliberately dumb — no ranking, no rotation,
/// no caching decisions here (that's SceneRankingService / SceneArchiveService, Phase 2-3).
/// Every method is windowed (skip/take) so the ~3900-row table is never loaded in one shot.
/// </summary>
public interface ISceneArchiveRepository
{
    /// <summary>Distinct scene_type values present in the archive — used to build rows.</summary>
    Task<List<string>> GetSceneTypesAsync(CancellationToken ct = default);

    /// <summary>
    /// One page of scenes for a given category, cheapest projection (no Chronicle/StitchedText
    /// text blobs). excludeSceneIds lets a caller keep a scene from appearing here if it's
    /// already been placed in another row (e.g. the Top-Ranked spotlight) on the same page load.
    /// </summary>
    Task<List<Scene>> GetScenePageAsync(string sceneType, int skip, int take, IReadOnlySet<int>? excludeSceneIds = null, CancellationToken ct = default);

    /// <summary>Highest-priority scenes across all categories, for the initial "hero" rows before any scrolling.</summary>
    Task<List<Scene>> GetTopScenesAsync(int take, CancellationToken ct = default);

    /// <summary>Full detail (including Chronicle text) for the fullscreen overlay. Single-row fetch.</summary>
    Task<Scene?> GetSceneDetailAsync(int sceneId, CancellationToken ct = default);

    /// <summary>Total scene count, for paging math / telemetry — cheap COUNT(*), not a full load.</summary>
    Task<int> GetSceneCountAsync(string? sceneType = null, CancellationToken ct = default);

    /// <summary>
    /// Searches the woven scene bank only (scene_name, teaser, chronicle, stitched_text, and
    /// book title) — never the raw ~498,000-chunk corpus. A name like "Erebus" that appears
    /// inside a scene's narration will match even if it's not in the title/teaser.
    /// </summary>
    Task<List<Scene>> SearchScenesAsync(string query, int take, CancellationToken ct = default);
}