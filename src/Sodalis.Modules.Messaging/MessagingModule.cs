using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Sodalis.Core;
using Sodalis.Modules.Messaging.Branding;
using Sodalis.Modules.Messaging.Persistence;
using Sodalis.Modules.Messaging.Providers;
using Sodalis.Modules.Messaging.Seeding;
using Sodalis.Modules.Messaging.Sending;
using Sodalis.Modules.Messaging.Settings;

namespace Sodalis.Modules.Messaging;

public sealed class MessagingModule : IModule
{
    public string Name => "Messaging";

    // TODO(api): IsRequired = true means Identity hard-depends on IMessageSender.
    // A host that doesn't want email at all has no way to opt out. Add a
    // NullMessageSender (no-op) and register it instead when
    // `Sodalis:Modules:Messaging:Enabled = false`, then drop IsRequired.
    public bool IsRequired => true;

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Sodalis")
            ?? throw new InvalidOperationException("ConnectionStrings:Sodalis is not configured.");

        services.AddDbContext<MessagingDbContext>(opts => opts
            .UseNpgsql(connectionString, npg => npg
                .MigrationsHistoryTable("__ef_migrations_history", MessagingDbContext.SchemaName)
                .MigrationsAssembly(typeof(MessagingDbContext).Assembly.FullName))
            .UseSnakeCaseNamingConvention());

        services.Configure<MessagingSettings>(configuration.GetSection(MessagingSettings.SectionName));

        services.AddMemoryCache();
        services.AddScoped<BrandingResolver>();
        services.AddSingleton<EmailTemplateRenderer>();
        services.AddScoped<MessagingSeeder>();

        // Single provider for now (SMTP). When Resend/SES land, add a config-driven
        // switch here based on MessagingSettings.Provider.
        services.AddScoped<IEmailProvider, SmtpEmailProvider>();

        services.AddScoped<IMessageSender, MessageSender>();

        services.ConfigureOpenTelemetryTracerProvider(t => t.AddSource(MessagingTelemetry.Name));
        services.ConfigureOpenTelemetryMeterProvider(m => m.AddMeter(MessagingTelemetry.Name));
    }

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        // No public endpoints — admin API is a separate scope.
    }

    public async Task ApplyMigrationsAsync(IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MessagingDbContext>();
        await db.Database.MigrateAsync(ct);

        var seeder = scope.ServiceProvider.GetRequiredService<MessagingSeeder>();
        await seeder.SeedAsync(ct);
    }
}
