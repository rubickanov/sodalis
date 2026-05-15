using Microsoft.EntityFrameworkCore;
using Sodalis.Modules.Identity.Domain;

namespace Sodalis.Modules.Identity.Persistence;

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : DbContext(options)
{
    public const string SchemaName = "identity";

    public DbSet<Player> Players => Set<Player>();
    public DbSet<ExternalIdentity> ExternalIdentities => Set<ExternalIdentity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
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
    }
}
