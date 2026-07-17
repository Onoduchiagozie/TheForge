namespace TheForge.Data.SceneArchive.Entities;

/// <summary>
/// Maps to the real "books" table in battle_scenes.db: book_id (TEXT), title, source_raw,
/// chunk_count. No series/canon/flag columns exist — this is a general Warhammer 40k corpus,
/// not Horus-Heresy-specific content, so there is deliberately no IsHorusHeresy (or any other
/// series) property here. A future Horus-Heresy-specific database swap can reintroduce
/// canon/priority logic then, scoped to that data — not guessed against this one.
/// </summary>
public class Book
{
    public string BookId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string SourceRaw { get; set; } = string.Empty;
    public int? ChunkCount { get; set; }

    public List<Scene> Scenes { get; set; } = [];
}
