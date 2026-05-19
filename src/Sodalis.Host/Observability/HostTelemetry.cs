using System.Diagnostics;

namespace Sodalis.Host.Observability;

internal static class HostTelemetry
{
    public const string Name = "Sodalis.Host";

    public static readonly ActivitySource ActivitySource = new(Name);
}
