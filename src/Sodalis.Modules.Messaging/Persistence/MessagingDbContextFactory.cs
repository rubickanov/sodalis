using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Sodalis.Modules.Messaging.Persistence;

internal sealed class MessagingDbContextFactory : IDesignTimeDbContextFactory<MessagingDbContext>
{
    public MessagingDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("SODALIS_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=sodalis;Username=sodalis;Password=sodalis";

        var options = new DbContextOptionsBuilder<MessagingDbContext>()
            .UseNpgsql(connectionString, npg => npg
                .MigrationsHistoryTable("__ef_migrations_history", MessagingDbContext.SchemaName)
                .MigrationsAssembly(typeof(MessagingDbContext).Assembly.FullName))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new MessagingDbContext(options);
    }
}
