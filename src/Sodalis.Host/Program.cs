using System.Globalization;
using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Context;
using Serilog.Enrichers.Span;
using Serilog.Events;
using Sodalis.Core;
using Sodalis.Host.Observability;
using Sodalis.Host.OpenApi;
using Sodalis.Modules.Identity;
using Sodalis.Modules.Messaging;
using Sodalis.Modules.Profile;
using Sodalis.Modules.Tenancy;

// Force English everywhere — error messages, formatting, FluentValidation translations.
// (i18n): if we ever want per-client localization, replace this with
//   app.UseRequestLocalization(...) reading Accept-Language.
var defaultCulture = CultureInfo.GetCultureInfo("en-US");
CultureInfo.DefaultThreadCurrentCulture = defaultCulture;
CultureInfo.DefaultThreadCurrentUICulture = defaultCulture;

// Bootstrap logger: captures fatal errors during host construction (DI, config, migrations)
// before the configured Serilog pipeline is wired up.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.WithEnvironmentName()
        .Enrich.WithSpan());

    // OTel pipeline must be registered BEFORE module RegisterServices — modules
    // add their own ActivitySource/Meter via ConfigureOpenTelemetryTracerProvider
    // and need the builder already wired up.
    builder.AddSodalisObservability();

    builder.Services.AddOpenApi("v1", opts =>
    {
        opts.AddDocumentTransformer((doc, _, _) =>
        {
            doc.Info.Title = "Sodalis API";
            doc.Info.Version = "v1";
            return Task.CompletedTask;
        });
        opts.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
        opts.AddOperationTransformer<BearerSecurityRequirementTransformer>();
    });

    builder.Services.AddSodalisCore(out ModuleRegistry moduleRegistry);
    // Tenancy MUST be first — it owns the API-key middleware that resolves
    // IGameContext for every /api/* request. Identity/Profile read from it.
    builder.Services.AddSodalisModule<TenancyModule>(moduleRegistry, builder.Configuration);
    builder.Services.AddSodalisModule<MessagingModule>(moduleRegistry, builder.Configuration);
    builder.Services.AddSodalisModule<IdentityModule>(moduleRegistry, builder.Configuration);
    builder.Services.AddSodalisModule<ProfileModule>(moduleRegistry, builder.Configuration);

    var connectionString = builder.Configuration.GetConnectionString("Sodalis")
        ?? throw new InvalidOperationException("ConnectionStrings:Sodalis is not configured.");

    builder.Services.AddHealthChecks()
        .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
        .AddNpgSql(connectionString, name: "postgres", tags: ["ready"]);

    var app = builder.Build();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference(opts => opts
            .WithTitle("Sodalis API")
            .AddPreferredSecuritySchemes("Bearer"));
    }

    app.UseSerilogRequestLogging(options =>
    {
        // Demote probe traffic to Verbose so it's filtered out at the default Information level.
        options.GetLevel = (httpContext, _, ex) =>
        {
            if (ex is not null || httpContext.Response.StatusCode >= 500)
                return LogEventLevel.Error;

            return httpContext.Request.Path.StartsWithSegments("/health")
                ? LogEventLevel.Verbose
                : LogEventLevel.Information;
        };

        // Surface tenant/player identity on the per-request summary line.
        // ApiKeyMiddleware sets the GameContext; JWT claims carry the player.
        options.EnrichDiagnosticContext = (diag, http) =>
        {
            var gameContext = http.RequestServices.GetService<IGameContext>();
            if (gameContext is { GameId: var gid } && gid != Guid.Empty)
                diag.Set("GameId", gid);

            var user = http.User;
            if (user.Identity?.IsAuthenticated == true)
            {
                var sub = user.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? user.FindFirstValue("sub");
                if (!string.IsNullOrEmpty(sub))
                    diag.Set("PlayerId", sub);

                var providers = user.FindFirstValue("auth");
                if (!string.IsNullOrEmpty(providers))
                    diag.Set("AuthProviders", providers);
            }
        };
    });

    using (HostTelemetry.ActivitySource.StartActivity("sodalis.migrate"))
    {
        await app.ApplySodalisMigrationsAsync();
    }

    app.ConfigureSodalisModules();

    app.UseAuthentication();
    app.UseAuthorization();

    // After authentication: push PlayerId to LogContext so every log line in
    // the request scope carries it. Keep it small — no DI, no allocation when
    // anonymous.
    app.Use(async (ctx, next) =>
    {
        var sub = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? ctx.User.FindFirstValue("sub");
        if (!string.IsNullOrEmpty(sub))
        {
            using (LogContext.PushProperty("PlayerId", sub))
                await next();
        }
        else
        {
            await next();
        }
    });

    var system = app.MapGroup("").WithTags("System");

    system.MapHealthChecks("/health/live", new HealthCheckOptions
    {
        Predicate = c => c.Tags.Contains("live")
    })
        .WithName("Liveness")
        .WithSummary("Liveness probe.")
        .WithDescription("Reports whether the process is responding. Does not check external dependencies. If this fails the orchestrator should restart the container.");

    system.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = c => c.Tags.Contains("ready")
    })
        .WithName("Readiness")
        .WithSummary("Readiness probe.")
        .WithDescription("Reports whether the service is ready to accept traffic. Checks the Postgres connection. If this fails the orchestrator should remove the pod from the load balancer (but NOT restart it).");

    system.MapGet("/version", () =>
    {
        var assembly = typeof(Program).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var version = assembly.GetName().Version?.ToString();

        string? commit = null;
        if (informational is not null)
        {
            var plus = informational.IndexOf('+');
            if (plus >= 0 && plus < informational.Length - 1)
            {
                commit = informational[(plus + 1)..];
            }
        }

        return Results.Ok(new
        {
            version,
            informationalVersion = informational,
            commit,
            buildTime = File.GetLastWriteTimeUtc(assembly.Location),
            runtime = Environment.Version.ToString(),
            environment = app.Environment.EnvironmentName
        });
    })
        .WithName("Version")
        .WithSummary("Build and runtime version info.")
        .WithDescription("Returns assembly version, git commit (if stamped at build), build timestamp, .NET runtime version, and environment name.");

    var v1 = app.MapGroup("/api/v1");
    v1.MapSodalisModules();

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program;
