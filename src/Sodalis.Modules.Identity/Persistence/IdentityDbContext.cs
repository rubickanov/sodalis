using Microsoft.EntityFrameworkCore;
using Sodalis.Modules.Identity.Domain;

namespace Sodalis.Modules.Identity.Persistence;

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : DbContext(options)
{
    public const string SchemaName = "identity";

    public DbSet<Player> Players => Set<Player>();
    public DbSet<ExternalIdentity> ExternalIdentities => Set<ExternalIdentity>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // TODO: Add HasQueryFilter on every entity (Player, ExternalIdentity, RefreshToken)
        // for tenant isolation defense-in-depth. Requires an injected IGameContext
        // (scoped service that resolves the current GameId from the request).
        // we promises this; current code relies on developer discipline only.

        modelBuilder.HasDefaultSchema(SchemaName);

        modelBuilder.Entity<Player>(e =>
        {
            e.HasKey(p => p.PlayerId);
            e.HasIndex(p => p.GameId);

            e.HasMany(p => p.ExternalIdentities)
                .WithOne()
                .HasForeignKey(ei => ei.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ExternalIdentity>(e =>
        {
            e.HasKey(ei => new { ei.PlayerId, ei.ProviderId });

            // One external account = one player per game
            e.HasIndex(ei => new { ei.GameId, ei.ProviderId, ei.ExternalId }).IsUnique();

            e.Property(ei => ei.Metadata).HasColumnType("jsonb");
        });

        modelBuilder.Entity<RefreshToken>(e =>
        {
            e.HasKey(rt => rt.TokenHash);

            // For "logout-all" and per-player session listing
            e.HasIndex(rt => new { rt.PlayerId, rt.GameId });

            // For background cleanup of expired tokens
            e.HasIndex(rt => rt.ExpiresAt);

            e.Property(rt => rt.TokenHash).HasMaxLength(128);
            e.Property(rt => rt.ReplacedByHash).HasMaxLength(128);
            e.Property(rt => rt.UserAgent).HasMaxLength(512);
            e.Property(rt => rt.IpAddress).HasMaxLength(64);
        });
    }
}
