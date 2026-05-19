using Microsoft.EntityFrameworkCore;
using Sodalis.Core;
using ProfileEntity = Sodalis.Modules.Profile.Domain.Profile;

namespace Sodalis.Modules.Profile.Persistence;

public sealed class ProfileDbContext(
    DbContextOptions<ProfileDbContext> options,
    IGameContext gameContext) : DbContext(options)
{
    public const string SchemaName = "profile";

    private readonly IGameContext _gameContext = gameContext;

    public DbSet<ProfileEntity> Profiles => Set<ProfileEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);

        modelBuilder.Entity<ProfileEntity>(e =>
        {
            // Composite PK lets one player have a profile per game and gives a
            // unique constraint that turns the read-then-insert race in
            // GetMyProfileHandler into a catchable DbUpdateException.
            e.HasKey(p => new { p.PlayerId, p.GameId });

            e.Property(p => p.DisplayName).HasMaxLength(64);
            e.Property(p => p.AvatarUrl).HasMaxLength(2048);

            e.HasQueryFilter(p => p.GameId == _gameContext.GameId);
        });
    }
}
