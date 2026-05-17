using Microsoft.EntityFrameworkCore;
using ProfileEntity = Sodalis.Modules.Profile.Domain.Profile;

namespace Sodalis.Modules.Profile.Persistence;

public sealed class ProfileDbContext(DbContextOptions<ProfileDbContext> options) : DbContext(options)
{
    public const string SchemaName = "profile";

    public DbSet<ProfileEntity> Profiles => Set<ProfileEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);

        modelBuilder.Entity<ProfileEntity>(e =>
        {
            e.HasKey(p => p.PlayerId);
            e.HasIndex(p => p.GameId);

            e.Property(p => p.DisplayName).HasMaxLength(64);
            e.Property(p => p.AvatarUrl).HasMaxLength(2048);
        });
    }
}
