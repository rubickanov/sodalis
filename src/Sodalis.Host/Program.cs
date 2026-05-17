using System.Reflection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;
using Sodalis.Core;
using Sodalis.Modules.Identity;
using Sodalis.Modules.Profile;

// Bootstrap logger: captures fatal errors during host construction (DI, config, migrations)
// before the configured Serilog pipeline is wired up.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // TODO(prod): add Serilog.Enrichers.Environment package and
    //   .Enrich.WithMachineName().Enrich.WithEnvironmentName()
    //   so logs from multiple pods are distinguishable in Loki/Elastic.
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    builder.Services.AddOpenApi("v1", opts =>
    {
        opts.AddDocumentTransformer((doc, _, _) =>
        {
            doc.Info.Title = "Sodalis API";
            doc.Info.Version = "v1";
            return Task.CompletedTask;
        });
    });

    builder.Services.AddSodalisCore(out ModuleRegistry moduleRegistry);
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
        app.MapScalarApiReference();
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
    });

    await app.ApplySodalisMigrationsAsync();

    app.UseAuthentication();
    app.UseAuthorization();

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
