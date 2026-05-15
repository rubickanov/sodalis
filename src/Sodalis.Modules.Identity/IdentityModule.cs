using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sodalis.Core;
using Sodalis.Modules.Identity.Auth;
using Sodalis.Modules.Identity.AuthProviders;
using Sodalis.Modules.Identity.Features.Login;
using Sodalis.Modules.Identity.Persistence;

namespace Sodalis.Modules.Identity;

public sealed class IdentityModule : IModule
{
    public string Name => "Identity";
    public bool IsRequired => true;

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // DbContext
        var connectionString = configuration.GetConnectionString("Sodalis")
            ?? throw new InvalidOperationException("ConnectionStrings:Sodalis is not configured.");

        services.AddDbContext<IdentityDbContext>(opts => opts
            .UseNpgsql(connectionString, npg => npg
                .MigrationsHistoryTable("__ef_migrations_history", IdentityDbContext.SchemaName)
                .MigrationsAssembly(typeof(IdentityDbContext).Assembly.FullName))
            .UseSnakeCaseNamingConvention());

        // JWT
        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.AddSingleton<JwtIssuer>();

        // Auth providers
        services.AddSingleton<IAuthProvider, AnonymousAuthProvider>();

        // Feature handlers
        services.AddScoped<LoginHandler>();
    }

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        LoginEndpoint.Map(routes);
    }

    public async Task ApplyMigrationsAsync(IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.Database.MigrateAsync(ct);
    }
}
