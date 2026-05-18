using Microsoft.EntityFrameworkCore;
using Sodalis.Modules.Tenancy.Domain;

namespace Sodalis.Modules.Tenancy.Persistence;

public sealed class TenancyDbContext(DbContextOptions<TenancyDbContext> options) : DbContext(options)
{
    public const string SchemaName = "tenancy";

    public DbSet<Game> Games => Set<Game>();
    public DbSet<GameApiKey> GameApiKeys => Set<GameApiKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);

        modelBuilder.Entity<Game>(e =>
        {
            e.HasKey(g => g.GameId);
            e.Property(g => g.Name).HasMaxLength(128);

            e.HasMany(g => g.ApiKeys)
                .WithOne()
                .HasForeignKey(k => k.GameId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GameApiKey>(e =>
        {
            e.HasKey(k => k.KeyHash);
            e.Property(k => k.KeyHash).HasMaxLength(64);
            e.Property(k => k.Prefix).HasMaxLength(16);
            e.Property(k => k.Name).HasMaxLength(64);

            e.HasIndex(k => k.GameId);

            // Hot-path lookup index — only matters for non-revoked keys.
            e.HasIndex(k => k.KeyHash)
                .HasFilter("revoked_at IS NULL")
                .HasDatabaseName("ix_game_api_keys_active");
        });
    }
}
