using Microsoft.EntityFrameworkCore;
using TheForge.Data.SceneArchive.Entities;

namespace TheForge.Data.SceneArchive;

/// <summary>
/// Read-only EF Core context over battle_scenes.db, mapped to the real schema confirmed via
/// dump_schema.py (books, build_progress, scene_chunks, scenes). build_progress and
/// scene_chunks are pipeline bookkeeping / source-chunk detail tables — not needed for the
/// archive UI, so they're deliberately not mapped here at all.
///
/// Explicit HasColumnName() calls throughout: the real columns are snake_case
/// (scene_id, book_id, scene_name, ...) while the C# properties are PascalCase by convention.
/// SQLite's case-insensitivity only covers letter casing, not the presence of underscores, so
/// without these mappings EF would generate SQL looking for columns like "SceneId" that don't
/// exist.
///
/// No migrations are registered on purpose: the schema is owned and written by the Python
/// pipeline, not by TheForge.
/// </summary>
public class SceneArchiveDbContext : DbContext
{
    public DbSet<Book> Books => Set<Book>();
    public DbSet<Scene> Scenes => Set<Scene>();

    public SceneArchiveDbContext(DbContextOptions<SceneArchiveDbContext> options) : base(options)
    {
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Book>(e =>
        {
            e.ToTable("books");
            e.HasKey(b => b.BookId);
            e.Property(b => b.BookId).HasColumnName("book_id");
            e.Property(b => b.Title).HasColumnName("title");
            e.Property(b => b.SourceRaw).HasColumnName("source_raw");
            e.Property(b => b.ChunkCount).HasColumnName("chunk_count");
        });

        modelBuilder.Entity<Scene>(e =>
        {
            e.ToTable("scenes");
            e.HasKey(s => s.SceneId);
            e.Property(s => s.SceneId).HasColumnName("scene_id");
            e.Property(s => s.BookId).HasColumnName("book_id");
            e.Property(s => s.SceneName).HasColumnName("scene_name");
            e.Property(s => s.Teaser).HasColumnName("teaser");
            e.Property(s => s.Chronicle).HasColumnName("chronicle");
            e.Property(s => s.QueryPrompt).HasColumnName("query_prompt");
            e.Property(s => s.StitchedText).HasColumnName("stitched_text");
            e.Property(s => s.SceneType).HasColumnName("scene_type");
            e.Property(s => s.Score).HasColumnName("score");
            e.Property(s => s.Rank).HasColumnName("rank");
            e.Property(s => s.SceneKey).HasColumnName("scene_key");
            e.Property(s => s.WovenAt).HasColumnName("woven_at");

            e.HasIndex(s => s.SceneType);
            e.HasIndex(s => new { s.Score, s.Rank });

            e.HasOne(s => s.Book)
             .WithMany(b => b.Scenes)
             .HasForeignKey(s => s.BookId)
             .OnDelete(DeleteBehavior.NoAction);
        });
    }
}
