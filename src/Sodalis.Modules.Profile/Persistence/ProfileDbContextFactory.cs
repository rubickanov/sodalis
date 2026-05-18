using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Sodalis.Core;

namespace Sodalis.Modules.Profile.Persistence;

internal sealed class ProfileDbContextFactory : IDesignTimeDbContextFactory<ProfileDbContext>
{
    public ProfileDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("SODALIS_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=sodalis;Username=sodalis;Password=sodalis";

        var options = new DbContextOptionsBuilder<ProfileDbContext>()
            .UseNpgsql(connectionString, npg => npg
                .MigrationsHistoryTable("__ef_migrations_history", ProfileDbContext.SchemaName)
                .MigrationsAssembly(typeof(ProfileDbContext).Assembly.FullName))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new ProfileDbContext(options, new EmptyGameContext());
    }

    private sealed class EmptyGameContext : IGameContext
    {
        public Guid GameId => Guid.Empty;
        public bool IsResolved => false;
    }
}
