using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Sodalis.Modules.Identity;

internal static class IdentityTelemetry
{
    public const string Name = "Sodalis.Modules.Identity";

    public static readonly ActivitySource ActivitySource = new(Name);
    public static readonly Meter Meter = new(Name);
}
