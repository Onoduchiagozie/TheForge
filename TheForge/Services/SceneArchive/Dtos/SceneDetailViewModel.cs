namespace TheForge.Services.SceneArchive.Dtos;

/// <summary>
/// Everything the fullscreen overlay needs. Author/Series were dropped — the real schema has
/// no such columns on "books" (title, source_raw, chunk_count only). BattleScore/OverallScore
/// collapsed into a single Score, NarrativeRank renamed to Rank, matching the real "scenes"
/// table's single score/rank columns. WovenAt stays a raw string — the on-disk text format
/// wasn't confirmed, so this avoids a DateTime parse failure at read time. No series/canon
/// concept exists in this general 40k database, so there's no IsHorusHeresy (or similar) here.
/// </summary>
public record SceneDetailViewModel(
    int Id,
    string Title,
    string BookTitle,
    string Teaser,
    string Chronicle,
    string? QueryPrompt,
    string SceneType,
    int AccentIndex,
    double Score,
    int Rank,
    string? WovenAt);
