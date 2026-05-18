using Microsoft.EntityFrameworkCore;
using Sodalis.Modules.Messaging.Domain;

namespace Sodalis.Modules.Messaging.Persistence;

public sealed class MessagingDbContext(DbContextOptions<MessagingDbContext> options) : DbContext(options)
{
    public const string SchemaName = "messaging";

    public DbSet<GameEmailBranding> GameEmailBrandings => Set<GameEmailBranding>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);

        modelBuilder.Entity<GameEmailBranding>(e =>
        {
            // GameId is opaque — no FK to tenancy.games. This keeps the module
            // extractable as a standalone service later without cross-DB joins.
            e.HasKey(b => b.GameId);
            e.Property(b => b.BrandName).HasMaxLength(128);
            e.Property(b => b.FromName).HasMaxLength(128);
            e.Property(b => b.ReplyTo).HasMaxLength(254);
            e.Property(b => b.LogoUrl).HasMaxLength(2048);
            e.Property(b => b.PrimaryColor).HasMaxLength(7);
            e.Property(b => b.SupportUrl).HasMaxLength(2048);
            e.Property(b => b.FooterText).HasMaxLength(512);
        });
    }
}
