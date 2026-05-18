using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Sodalis.Modules.Messaging;

internal static class MessagingTelemetry
{
    public const string Name = "Sodalis.Modules.Messaging";

    public static readonly ActivitySource ActivitySource = new(Name);
    public static readonly Meter Meter = new(Name);
}
