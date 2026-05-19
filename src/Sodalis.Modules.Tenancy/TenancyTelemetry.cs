using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Sodalis.Modules.Tenancy;

internal static class TenancyTelemetry
{
    public const string Name = "Sodalis.Modules.Tenancy";

    public static readonly ActivitySource ActivitySource = new(Name);
    public static readonly Meter Meter = new(Name);

    public static readonly Counter<long> ApiKeyResolutionTotal =
        Meter.CreateCounter<long>("sodalis.tenancy.apikey_resolution_total", description: "API key resolution attempts (outcome=hit|miss|invalid, source=cache|db).");
}
