using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Sodalis.Core;

namespace Sodalis.Modules.Identity.Persistence;

internal sealed class IdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("SODALIS_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=sodalis;Username=sodalis;Password=sodalis";

        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(connectionString, npg => npg
                .MigrationsHistoryTable("__ef_migrations_history", IdentityDbContext.SchemaName)
                .MigrationsAssembly(typeof(IdentityDbContext).Assembly.FullName))
            .UseSnakeCaseNamingConvention()
            .Options;

        // Design-time only — runtime never uses this factory. Empty context here
        // makes HasQueryFilter parameters compile-time valid; migrations don't
        // execute filtered queries.
        return new IdentityDbContext(options, new EmptyGameContext());
    }

    private sealed class EmptyGameContext : IGameContext
    {
        public Guid GameId => Guid.Empty;
        public bool IsResolved => false;
    }
}
