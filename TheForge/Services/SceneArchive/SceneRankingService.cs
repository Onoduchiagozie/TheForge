using System.Globalization;
using Microsoft.Extensions.Caching.Memory;
using TheForge.Data.SceneArchive.Entities;

namespace TheForge.Services.SceneArchive;

public class SceneRankingService(ISceneArchiveRepository repository, IMemoryCache cache) : ISceneRankingService
{
    // How often the featured rotation reshuffles. Everyone browsing within the same window
    // sees the same order (a reload mid-window shouldn't feel random/broken); a new window
    // means a new shuffle, which is what surfaces different content to repeat visitors.
    private static readonly TimeSpan RotationWindow = TimeSpan.FromMinutes(20);

    // Candidate pool sizes per row, pulled via the repository's card projection (no Chronicle
    // text). Keeps memory bounded regardless of how many thousand scenes exist for a category.
    private const int CandidatePoolPerRow = 60;
    private const int TopScenesPoolSize = 80;

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

        // Row 0: pinned spotlight — the highest score/rank scenes across the WHOLE corpus,
        // no series/book filtering of any kind. This is a general 40k database; "best first"
        // means highest score, full stop. GetTopScenesAsync already orders by Score DESC,
        // Rank ASC at the SQL level, so this pool is already the best of the best.
        var topScenes = await repository.GetTopScenesAsync(TopScenesPoolSize, ct);
        if (topScenes.Count > 0)
        {
            plans.Add(new RowPlan(
                Key: "top-ranked",
                DisplayLabel: "Top-Ranked Chronicles",
                Direction: ScrollDirection.Left,
                Scenes: WeightedShuffle(topScenes, seed)));
        }

        // One row per remaining scene_type, alternating scroll direction so adjacent rows
        // are visually distinct in motion as well as accent color.
        var sceneTypes = await repository.GetSceneTypesAsync(ct);
        var rowIndex = plans.Count;

        foreach (var sceneType in sceneTypes)
        {
            ct.ThrowIfCancellationRequested();

            var pool = await repository.GetScenePageAsync(sceneType, 0, CandidatePoolPerRow, ct);
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
    /// Efraimidis–Spirakis weighted random sampling. Input arrives priority-ordered (best
    /// first) from the repository. Each item gets weight 1/(position+1); final order is
    /// descending by key = rand^(1/weight), seeded per rotation window. High-priority items
    /// land near the front far more often than not, but which exact items and what order
    /// varies by window — that's the "discover new content" behavior. Deliberately NOT a
    /// uniform shuffle: a pure shuffle would bury the best scenes as often as it surfaces them.
    /// </summary>
    private static List<Scene> WeightedShuffle(List<Scene> priorityOrdered, int seed)
    {
        var rng = new Random(seed);
        return priorityOrdered
            .Select((scene, index) =>
            {
                var weight = 1.0 / (index + 1);
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
