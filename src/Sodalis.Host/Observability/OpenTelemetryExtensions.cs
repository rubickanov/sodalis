using System.Reflection;
using Npgsql;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Sodalis.Host.Observability;

internal static class OpenTelemetryExtensions
{
    public static WebApplicationBuilder AddSodalisObservability(this WebApplicationBuilder builder)
    {
        // Integration tests don't have an OTLP target up and exporting on every
        // request would slow the suite (and spam connection-refused warnings).
        // Modules still register their ActivitySource/Meter — only the exporter
        // is skipped, so in-memory exporters can be wired in later if needed.
        var isTest = builder.Environment.IsEnvironment("Test");

        var assembly = Assembly.GetExecutingAssembly();
        var serviceVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "unknown";

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(
                serviceName: "sodalis",
                serviceVersion: serviceVersion,
                serviceInstanceId: Environment.MachineName))
            .WithTracing(t =>
            {
                t.AddSource(HostTelemetry.Name);
                t.AddAspNetCoreInstrumentation(o =>
                {
                    // Health probes hammer the endpoint — keep them out of traces.
                    o.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health");
                });
                t.AddHttpClientInstrumentation();
                t.AddNpgsql();

                if (!isTest)
                    t.AddOtlpExporter();
            })
            .WithMetrics(m =>
            {
                m.AddAspNetCoreInstrumentation();
                m.AddHttpClientInstrumentation();
                m.AddRuntimeInstrumentation();

                if (!isTest)
                    m.AddOtlpExporter();
            });

        return builder;
    }
}
