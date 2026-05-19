using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Sodalis.Modules.Messaging;

internal static class MessagingTelemetry
{
    public const string Name = "Sodalis.Modules.Messaging";

    public static readonly ActivitySource ActivitySource = new(Name);
    public static readonly Meter Meter = new(Name);

    public static readonly Counter<long> EmailSendTotal =
        Meter.CreateCounter<long>("sodalis.messaging.email_send_total", description: "Email send attempts (kind, outcome=sent|retried|failed|skipped).");

    public static readonly Histogram<double> EmailSendDurationSeconds =
        Meter.CreateHistogram<double>("sodalis.messaging.email_send_duration_seconds", unit: "s", description: "End-to-end email send duration including retries.");
}
