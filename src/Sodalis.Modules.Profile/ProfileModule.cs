using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Sodalis.Core;
using Sodalis.Modules.Profile.Features.GetMyProfile;
using Sodalis.Modules.Profile.Features.GetProfileById;
using Sodalis.Modules.Profile.Features.UpdateMyProfile;
using Sodalis.Modules.Profile.Persistence;

namespace Sodalis.Modules.Profile;

public sealed class ProfileModule : IModule
{
    public string Name => "Profile";
    public bool IsRequired => false;

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Sodalis")
            ?? throw new InvalidOperationException("ConnectionStrings:Sodalis is not configured.");

        services.AddDbContext<ProfileDbContext>(opts => opts
            .UseNpgsql(connectionString, npg => npg
                .MigrationsHistoryTable("__ef_migrations_history", ProfileDbContext.SchemaName)
                .MigrationsAssembly(typeof(ProfileDbContext).Assembly.FullName))
            .UseSnakeCaseNamingConvention());

        services.AddScoped<GetMyProfileHandler>();
        services.AddScoped<UpdateMyProfileHandler>();

        services.AddValidatorsFromAssemblyContaining<ProfileModule>(ServiceLifetime.Singleton);

        services.ConfigureOpenTelemetryTracerProvider(t => t.AddSource(ProfileTelemetry.Name));
        services.ConfigureOpenTelemetryMeterProvider(m => m.AddMeter(ProfileTelemetry.Name));
    }

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var profile = routes.MapGroup("/profile").WithTags("Profile");

        GetMyProfileEndpoint.Map(profile);
        UpdateMyProfileEndpoint.Map(profile);
        GetProfileByIdEndpoint.Map(profile);
    }

    public async Task ApplyMigrationsAsync(IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ProfileDbContext>();
        await db.Database.MigrateAsync(ct);
    }
}
