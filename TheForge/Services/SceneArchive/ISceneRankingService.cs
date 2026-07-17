namespace TheForge.Services.SceneArchive;

/// <summary>
/// Turns raw repository queries into the ordered set of rows the landing page renders:
/// priority ranking (Horus Heresy popularity → battle score → overall score → narrative rank)
/// plus a periodically-reshuffled "featured" rotation so repeat visitors see new orderings
/// instead of the exact same front row every time.
/// </summary>
public interface ISceneRankingService
{
    /// <summary>
    /// Builds all rows for the landing page: a pinned Horus Heresy spotlight row first, then
    /// one row per remaining scene_type, alternating scroll direction. Cached per rotation
    /// window (see SceneRankingService.RotationWindow) so concurrent requests within the same
    /// window don't each hit the database.
    /// </summary>
    Task<List<RowPlan>> GetRowPlansAsync(CancellationToken ct = default);

    /// <summary>Expires the current rotation window early — e.g. a manual "reshuffle now" trigger.</summary>
    void InvalidateRotation();
}
