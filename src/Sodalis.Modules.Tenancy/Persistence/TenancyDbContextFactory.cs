using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Sodalis.Modules.Tenancy.Persistence;

internal sealed class TenancyDbContextFactory : IDesignTimeDbContextFactory<TenancyDbContext>
{
    public TenancyDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("SODALIS_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=sodalis;Username=sodalis;Password=sodalis";

        var options = new DbContextOptionsBuilder<TenancyDbContext>()
            .UseNpgsql(connectionString, npg => npg
                .MigrationsHistoryTable("__ef_migrations_history", TenancyDbContext.SchemaName)
                .MigrationsAssembly(typeof(TenancyDbContext).Assembly.FullName))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new TenancyDbContext(options);
    }
}
