namespace TheForge.Services.SceneArchive.Dtos;

/// <summary>
/// What a ticker card actually renders. Deliberately thin — no Chronicle/StitchedText —
/// so a row of 60 cards never drags full narration text through the component tree.
///
/// IsFeatured is generic and score-driven (true only for cards in the pinned "Top-Ranked
/// Chronicles" row) — it carries no series/book semantics. There is no series/canon concept
/// in this general 40k database.
/// </summary>
public record SceneCardSummary(
    int Id,
    string Title,
    string BookTitle,
    string Teaser,
    string SceneType,
    bool IsFeatured,
    int AccentIndex);
