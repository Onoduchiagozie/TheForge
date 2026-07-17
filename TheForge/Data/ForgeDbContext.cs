// using Microsoft.EntityFrameworkCore;
// using TheForge.Data.Entities;
//
// namespace TheForge.Data;
//
// public class ForgeDbContext(DbContextOptions<ForgeDbContext> options) : DbContext(options)
// {
//     public DbSet<User> Users => Set<User>();
//     public DbSet<UserSettings> UserSettings => Set<UserSettings>();
//     public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
//     public DbSet<UsageLog> UsageLogs => Set<UsageLog>();
//
//     protected override void OnModelCreating(ModelBuilder modelBuilder)
//     {
//         // User
//         modelBuilder.Entity<User>(e =>
//         {
//             e.HasKey(u => u.Id);
//             e.HasIndex(u => u.Email).IsUnique();
//             e.Property(u => u.Tier).HasDefaultValue("free");
//         });
//
//         // UserSettings — one-to-one with User
//         modelBuilder.Entity<UserSettings>(e =>
//         {
//             e.HasKey(s => s.Id);
//             e.HasOne(s => s.User)
//              .WithOne(u => u.Settings)
//              .HasForeignKey<UserSettings>(s => s.UserId)
//              .OnDelete(DeleteBehavior.Cascade);
//         });
//
//         // ApiKey — many per user
//         modelBuilder.Entity<ApiKey>(e =>
//         {
//             e.HasKey(k => k.Id);
//             e.HasIndex(k => k.KeyHash).IsUnique();
//             e.HasOne(k => k.User)
//              .WithMany(u => u.ApiKeys)
//              .HasForeignKey(k => k.UserId)
//              .OnDelete(DeleteBehavior.Cascade);
//         });
//
//         // UsageLog — many per user, indexed for daily count queries
//         modelBuilder.Entity<UsageLog>(e =>
//         {
//             e.HasKey(l => l.Id);
//             e.HasIndex(l => new { l.UserId, l.Timestamp });
//             e.HasOne(l => l.User)
//              .WithMany(u => u.UsageLogs)
//              .HasForeignKey(l => l.UserId)
//              .OnDelete(DeleteBehavior.Cascade);
//         });
//
//         // Seed: default admin user with API key "forge-dev-key" (dev only)
//         modelBuilder.Entity<User>().HasData(new User
//         {
//             Id = 1,
//             Email = "admin@theforge.dev",
//             PasswordHash = "dev-only-not-hashed",
//             Tier = "archmagos",
//             CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
//             IsActive = true,
//         });
//
//         modelBuilder.Entity<UserSettings>().HasData(new UserSettings
//         {
//             Id = 1,
//             UserId = 1,
//             PreferredMode = "remembrancer",
//             UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
//         });
//
//         // Seed a dev API key — hash of "forge-dev-key"
//         modelBuilder.Entity<ApiKey>().HasData(new ApiKey
//         {
//             Id = 1,
//             UserId = 1,
//             KeyHash = "c1c0e2b2d2e8f0a3c9b7d5e6f4a8b2c0d3e7f1a5b9c2d6e0f4a8b2c0d3e7f1a5",
//             Name = "Dev key",
//             CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
//             IsActive = true,
//         });
//     }
// }
