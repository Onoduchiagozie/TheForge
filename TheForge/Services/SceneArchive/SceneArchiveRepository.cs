using Microsoft.EntityFrameworkCore;
using TheForge.Data.SceneArchive;
using TheForge.Data.SceneArchive.Entities;

namespace TheForge.Services.SceneArchive;

public class SceneArchiveRepository(SceneArchiveDbContext db) : ISceneArchiveRepository
{
    // Card-list projection: everything a ticker card / row needs, nothing else. Chronicle and
    // StitchedText (the big text blobs) are deliberately left out of the SQL — only
    // GetSceneDetailAsync pulls those, one row at a time, when a card is actually opened.
    private static readonly Func<IQueryable<Scene>, IQueryable<Scene>> CardProjection = q =>
        q.Select(s => new Scene
        {
            SceneId = s.SceneId,
            BookId = s.BookId,
            SceneName = s.SceneName,
            Teaser = s.Teaser,
            SceneType = s.SceneType,
            Score = s.Score,
            Rank = s.Rank,
            WovenAt = s.WovenAt,
            Book = new Book
            {
                BookId = s.Book.BookId,
                Title = s.Book.Title,
                SourceRaw = s.Book.SourceRaw,
                ChunkCount = s.Book.ChunkCount,
            },
        });

    // Pure score/rank ordering — this is a general 40k corpus with no series/canon metadata,
    // so "best scenes first" just means highest score, tie-broken by rank. No series-based
    // logic belongs here; if a future Horus-Heresy-specific database adds real canon/priority
    // columns, that ordering can be added then, scoped to that data.
    private static IOrderedQueryable<Scene> ByPriority(IQueryable<Scene> q) => q
        .OrderByDescending(s => s.Score)
        .ThenBy(s => s.Rank);

    public async Task<List<string>> GetSceneTypesAsync(CancellationToken ct = default) =>
        await db.Scenes
            .Where(s => s.SceneType != null && s.SceneType != "")
            .Select(s => s.SceneType!)
            .Distinct()
            .OrderBy(t => t)
            .ToListAsync(ct);

    public async Task<List<Scene>> GetScenePageAsync(string sceneType, int skip, int take, IReadOnlySet<int>? excludeSceneIds = null, CancellationToken ct = default)
    {
        var query = db.Scenes.Where(s => s.SceneType == sceneType);

        if (excludeSceneIds is { Count: > 0 })
            query = query.Where(s => !excludeSceneIds.Contains(s.SceneId));

        query = ByPriority(query);
        return await CardProjection(query).Skip(skip).Take(take).ToListAsync(ct);
    }

    public async Task<List<Scene>> GetTopScenesAsync(int take, CancellationToken ct = default)
    {
        var query = ByPriority(db.Scenes);
        return await CardProjection(query).Take(take).ToListAsync(ct);
    }

    public async Task<Scene?> GetSceneDetailAsync(int sceneId, CancellationToken ct = default) =>
        await db.Scenes
            .Include(s => s.Book)
            .FirstOrDefaultAsync(s => s.SceneId == sceneId, ct);

    public async Task<int> GetSceneCountAsync(string? sceneType = null, CancellationToken ct = default) =>
        sceneType is null
            ? await db.Scenes.CountAsync(ct)
            : await db.Scenes.CountAsync(s => s.SceneType == sceneType, ct);

    // Widened to search Book.Title and StitchedText as well as scene_name/teaser/chronicle —
    // a user might search a book name, not just a character. Still only ever the woven scene
    // bank (scenes + books), never the raw ~498,000-chunk corpus.
    public async Task<List<Scene>> SearchScenesAsync(string query, int take, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        // Escape SQL LIKE wildcards in the user's input so a literal "%" or "_" in a search
        // term behaves as a literal character instead of a wildcard.
        var escaped = query.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
        var pattern = $"%{escaped}%";

        var matches = db.Scenes.Where(s =>
            (s.SceneName != null && EF.Functions.Like(s.SceneName, pattern, "\\")) ||
            (s.Teaser != null && EF.Functions.Like(s.Teaser, pattern, "\\")) ||
            (s.Chronicle != null && EF.Functions.Like(s.Chronicle, pattern, "\\")) ||
            (s.StitchedText != null && EF.Functions.Like(s.StitchedText, pattern, "\\")) ||
            EF.Functions.Like(s.Book.Title, pattern, "\\"));

        matches = ByPriority(matches);
        return await CardProjection(matches).Take(take).ToListAsync(ct);
    }
}