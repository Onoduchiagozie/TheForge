using TheForge.Data.SceneArchive.Entities;
using TheForge.Services.SceneArchive.Dtos;

namespace TheForge.Services.SceneArchive;

public class SceneArchiveService(ISceneRankingService rankingService, ISceneArchiveRepository repository)
    : ISceneArchiveService
{
    public async Task<List<SceneRowViewModel>> GetLandingRowsAsync(CancellationToken ct = default)
    {
        var rowPlans = await rankingService.GetRowPlansAsync(ct);

        return rowPlans
            .Select(row => new SceneRowViewModel(
                Key: row.Key,
                DisplayLabel: row.DisplayLabel,
                Direction: row.Direction,
                // IsFeatured is purely "this card is in the pinned top-score row" — no
                // series/book meaning attached. General 40k data, general "best first" ranking.
                Cards: row.Scenes.Select(scene => ToCardSummary(scene, isFeatured: row.Key == "top-ranked")).ToList()))
            .ToList();
    }

    public async Task<SceneDetailViewModel?> GetSceneDetailAsync(int sceneId, CancellationToken ct = default)
    {
        var scene = await repository.GetSceneDetailAsync(sceneId, ct);
        return scene is null ? null : ToDetailViewModel(scene);
    }

    private const int SearchResultLimit = 40;

    public async Task<List<SceneCardSummary>> SearchScenesAsync(string query, CancellationToken ct = default)
    {
        var matches = await repository.SearchScenesAsync(query, SearchResultLimit, ct);
        return matches.Select(scene => ToCardSummary(scene, isFeatured: false)).ToList();
    }

    public Task<int> GetTotalSceneCountAsync(CancellationToken ct = default) =>
        repository.GetSceneCountAsync(null, ct);

    private static SceneCardSummary ToCardSummary(Scene scene, bool isFeatured)
    {
        var sceneType = scene.SceneType ?? "unclassified";
        return new SceneCardSummary(
            Id: scene.SceneId,
            Title: scene.SceneName ?? "Untitled Chronicle",
            BookTitle: scene.Book.Title,
            Teaser: scene.Teaser ?? string.Empty,
            SceneType: sceneType,
            IsFeatured: isFeatured,
            AccentIndex: AccentPalette.GetAccentIndex(sceneType));
    }

    private static SceneDetailViewModel ToDetailViewModel(Scene scene)
    {
        var sceneType = scene.SceneType ?? "unclassified";
        return new SceneDetailViewModel(
            Id: scene.SceneId,
            Title: scene.SceneName ?? "Untitled Chronicle",
            BookTitle: scene.Book.Title,
            Teaser: scene.Teaser ?? string.Empty,
            Chronicle: scene.Chronicle ?? "(No narration recorded for this scene.)",
            QueryPrompt: scene.QueryPrompt,
            SceneType: sceneType,
            AccentIndex: AccentPalette.GetAccentIndex(sceneType),
            Score: scene.Score,
            Rank: scene.Rank,
            WovenAt: scene.WovenAt);
    }
}