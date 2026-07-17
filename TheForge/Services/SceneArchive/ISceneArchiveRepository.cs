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

    /// <summary>One page of scenes for a given category, cheapest projection (no Chronicle/StitchedText text blobs).</summary>
    Task<List<Scene>> GetScenePageAsync(string sceneType, int skip, int take, CancellationToken ct = default);

    /// <summary>Highest-priority scenes across all categories, for the initial "hero" rows before any scrolling.</summary>
    Task<List<Scene>> GetTopScenesAsync(int take, CancellationToken ct = default);

    /// <summary>Full detail (including Chronicle text) for the fullscreen overlay. Single-row fetch.</summary>
    Task<Scene?> GetSceneDetailAsync(int sceneId, CancellationToken ct = default);

    /// <summary>Total scene count, for paging math / telemetry — cheap COUNT(*), not a full load.</summary>
    Task<int> GetSceneCountAsync(string? sceneType = null, CancellationToken ct = default);
}
