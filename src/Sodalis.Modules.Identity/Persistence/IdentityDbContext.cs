using Microsoft.EntityFrameworkCore;
using Sodalis.Core;
using Sodalis.Modules.Identity.Domain;

namespace Sodalis.Modules.Identity.Persistence;

public sealed class IdentityDbContext(
    DbContextOptions<IdentityDbContext> options,
    IGameContext gameContext) : DbContext(options)
{
    public const string SchemaName = "identity";

    private readonly IGameContext _gameContext = gameContext;

    public DbSet<Player> Players => Set<Player>();
    public DbSet<ExternalIdentity> ExternalIdentities => Set<ExternalIdentity>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<EmailVerificationToken> EmailVerificationTokens => Set<EmailVerificationToken>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

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

            e.HasQueryFilter(p => p.GameId == _gameContext.GameId);
        });

        modelBuilder.Entity<ExternalIdentity>(e =>
        {
            e.HasKey(ei => new { ei.PlayerId, ei.ProviderId });

            // One external account = one player per game
            e.HasIndex(ei => new { ei.GameId, ei.ProviderId, ei.ExternalId }).IsUnique();

            e.Property(ei => ei.Metadata).HasColumnType("jsonb");

            e.HasQueryFilter(ei => ei.GameId == _gameContext.GameId);
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

            e.HasQueryFilter(rt => rt.GameId == _gameContext.GameId);
        });

        modelBuilder.Entity<EmailVerificationToken>(e =>
        {
            e.HasKey(t => t.TokenHash);
            e.HasIndex(t => new { t.PlayerId, t.GameId });

            e.Property(t => t.TokenHash).HasMaxLength(64);
            e.Property(t => t.Email).HasMaxLength(254);

            e.HasQueryFilter(t => t.GameId == _gameContext.GameId);
        });

        modelBuilder.Entity<PasswordResetToken>(e =>
        {
            e.HasKey(t => t.TokenHash);
            e.HasIndex(t => new { t.PlayerId, t.GameId });

            e.Property(t => t.TokenHash).HasMaxLength(64);
            e.Property(t => t.IpAddress).HasMaxLength(64);

            e.HasQueryFilter(t => t.GameId == _gameContext.GameId);
        });
    }
}
