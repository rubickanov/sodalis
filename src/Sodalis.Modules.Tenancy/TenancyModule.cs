using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Sodalis.Core;
using Sodalis.Modules.Tenancy.ApiKeys;
using Sodalis.Modules.Tenancy.Persistence;
using Sodalis.Modules.Tenancy.Seeding;

namespace Sodalis.Modules.Tenancy;

public sealed class TenancyModule : IModule
{
    public string Name => "Tenancy";
    public bool IsRequired => true;

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Sodalis")
            ?? throw new InvalidOperationException("ConnectionStrings:Sodalis is not configured.");

        services.AddDbContext<TenancyDbContext>(opts => opts
            .UseNpgsql(connectionString, npg => npg
                .MigrationsHistoryTable("__ef_migrations_history", TenancyDbContext.SchemaName)
                .MigrationsAssembly(typeof(TenancyDbContext).Assembly.FullName))
            .UseSnakeCaseNamingConvention());

        services.Configure<TenancySeedSettings>(configuration.GetSection(TenancySeedSettings.SectionName));

        services.AddMemoryCache();
        services.AddScoped<ApiKeyResolver>();
        services.AddScoped<TenancySeeder>();

        services.ConfigureOpenTelemetryTracerProvider(t => t.AddSource(TenancyTelemetry.Name));
        services.ConfigureOpenTelemetryMeterProvider(m => m.AddMeter(TenancyTelemetry.Name));
    }

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        // Tenancy has no public endpoints in this round (admin API is a separate scope).
    }

    public void ConfigureMiddleware(IApplicationBuilder app)
    {
        // Only requests under /api/* require an API key. Health, version, openapi,
        // and scalar stay open.
        app.UseWhen(
            ctx => ctx.Request.Path.StartsWithSegments("/api"),
            branch => branch.UseMiddleware<ApiKeyMiddleware>());
    }

    public async Task ApplyMigrationsAsync(IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TenancyDbContext>();
        await db.Database.MigrateAsync(ct);

        var seeder = scope.ServiceProvider.GetRequiredService<TenancySeeder>();
        await seeder.SeedAsync(ct);
    }
}
