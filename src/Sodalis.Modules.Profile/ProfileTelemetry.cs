using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Sodalis.Modules.Profile;

internal static class ProfileTelemetry
{
    public const string Name = "Sodalis.Modules.Profile";

    public static readonly ActivitySource ActivitySource = new(Name);
    public static readonly Meter Meter = new(Name);
}
