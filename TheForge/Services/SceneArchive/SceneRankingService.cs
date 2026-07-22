using System.Globalization;
using Microsoft.Extensions.Caching.Memory;
using TheForge.Data.SceneArchive.Entities;

namespace TheForge.Services.SceneArchive;

public class SceneRankingService(ISceneArchiveRepository repository, IMemoryCache cache) : ISceneRankingService
{
    // Shortened from 20 to 15 minutes — combined with the softer shuffle weighting below,
    // this means noticeably more turnover in what's visible, not the same handful of top
    // scorers parked in the front slot for most of an hour.
    private static readonly TimeSpan RotationWindow = TimeSpan.FromMinutes(15);

    // Widened significantly from the original 60/80 — against a ~6,180-row scenes table,
    // pools that small meant most of the archive was never even in the running to be shown.
    private const int CandidatePoolPerRow = 140;
    private const int TopScenesPoolSize = 160;

    private const string CacheKeyPrefix = "scene-archive:rowplans:";

    public async Task<List<RowPlan>> GetRowPlansAsync(CancellationToken ct = default)
    {
        var epoch = CurrentEpoch();
        var cacheKey = CacheKeyPrefix + epoch;

        if (cache.TryGetValue(cacheKey, out List<RowPlan>? cached) && cached is not null)
            return cached;

        var plans = await BuildRowPlansAsync(epoch, ct);
        cache.Set(cacheKey, plans, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = RotationWindow,
            Size = 1, // required — the app's shared IMemoryCache has SizeLimit set in Program.cs
        });
        return plans;
    }

    public void InvalidateRotation() => cache.Remove(CacheKeyPrefix + CurrentEpoch());

    private async Task<List<RowPlan>> BuildRowPlansAsync(long epoch, CancellationToken ct)
    {
        var plans = new List<RowPlan>();
        var seed = unchecked((int)epoch);

        // Row 0: pinned spotlight — highest score/rank scenes across the WHOLE corpus.
        var topScenes = await repository.GetTopScenesAsync(TopScenesPoolSize, ct);
        if (topScenes.Count > 0)
        {
            plans.Add(new RowPlan(
                Key: "top-ranked",
                DisplayLabel: "Top-Ranked Chronicles",
                Direction: ScrollDirection.Left,
                Scenes: WeightedShuffle(topScenes, seed)));
        }

        // A scene already placed in the Top-Ranked row is excluded from its own scene_type
        // row below — previously the same standout scene (e.g. a single unusually high-score
        // Erebus scene) would appear in BOTH the spotlight row and its category row on every
        // single page load, which is a big part of why it felt omnipresent.
        var topSceneIds = topScenes.Select(s => s.SceneId).ToHashSet();

        var sceneTypes = await repository.GetSceneTypesAsync(ct);
        var rowIndex = plans.Count;

        foreach (var sceneType in sceneTypes)
        {
            ct.ThrowIfCancellationRequested();

            var pool = await repository.GetScenePageAsync(sceneType, 0, CandidatePoolPerRow, topSceneIds, ct);
            if (pool.Count == 0)
                continue;

            plans.Add(new RowPlan(
                Key: sceneType,
                DisplayLabel: ToDisplayLabel(sceneType),
                Direction: rowIndex % 2 == 0 ? ScrollDirection.Left : ScrollDirection.Right,
                Scenes: WeightedShuffle(pool, seed + rowIndex)));

            rowIndex++;
        }

        return plans;
    }

    /// <summary>
    /// Weighted random sampling (Efraimidis–Spirakis), same technique as before but with a
    /// softer weight curve: 1/sqrt(position+1) instead of 1/(position+1). The old formula gave
    /// the single top-scored item in a pool such a dominant weight that it landed in/near the
    /// front slot almost every rotation window — which is exactly the "I keep seeing the same
    /// scene" complaint. The sqrt curve still favors higher-scored scenes overall, but gives
    /// mid-tier items in the pool a real chance to surface too.
    /// </summary>
    private static List<Scene> WeightedShuffle(List<Scene> priorityOrdered, int seed)
    {
        var rng = new Random(seed);
        return priorityOrdered
            .Select((scene, index) =>
            {
                var weight = 1.0 / Math.Sqrt(index + 1);
                var key = Math.Pow(rng.NextDouble(), 1.0 / weight);
                return (scene, key);
            })
            .OrderByDescending(x => x.key)
            .Select(x => x.scene)
            .ToList();
    }

    private static long CurrentEpoch() =>
        DateTimeOffset.UtcNow.ToUnixTimeSeconds() / (long)RotationWindow.TotalSeconds;

    private static string ToDisplayLabel(string sceneType) =>
        string.IsNullOrWhiteSpace(sceneType)
            ? "Unclassified Engagements"
            : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(sceneType.Replace('-', ' ').Replace('_', ' '));
}