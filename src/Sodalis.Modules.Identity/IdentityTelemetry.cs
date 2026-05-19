using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Sodalis.Modules.Identity;

internal static class IdentityTelemetry
{
    public const string Name = "Sodalis.Modules.Identity";

    public static readonly ActivitySource ActivitySource = new(Name);
    public static readonly Meter Meter = new(Name);

    public static readonly Counter<long> LoginTotal =
        Meter.CreateCounter<long>("sodalis.identity.login_total", description: "Login attempts (success/failure).");

    public static readonly Counter<long> RegistrationsTotal =
        Meter.CreateCounter<long>("sodalis.identity.registrations_total", description: "Player registration attempts.");

    public static readonly Counter<long> RefreshTotal =
        Meter.CreateCounter<long>("sodalis.identity.refresh_total", description: "Refresh-token rotation attempts.");

    // Standalone tripwire — keeps a clean alerting series isolated from refresh_total.
    public static readonly Counter<long> RefreshReuseDetectedTotal =
        Meter.CreateCounter<long>("sodalis.identity.refresh_reuse_detected_total", description: "Refresh-token reuse detections (possible session compromise).");

    public static readonly Counter<long> TokenIssuanceTotal =
        Meter.CreateCounter<long>("sodalis.identity.token_issuance_total", description: "Tokens issued (kind=access|refresh).");

    public static readonly Counter<long> PasswordResetRequestedTotal =
        Meter.CreateCounter<long>("sodalis.identity.password_reset_requested_total", description: "Password reset requests (outcome=issued|unknown_email).");

    public static readonly Counter<long> PasswordResetCompletedTotal =
        Meter.CreateCounter<long>("sodalis.identity.password_reset_completed_total", description: "Password reset completions.");

    public static readonly Counter<long> EmailVerifiedTotal =
        Meter.CreateCounter<long>("sodalis.identity.email_verified_total", description: "Email verification attempts.");

    public static readonly Counter<long> PasswordChangedTotal =
        Meter.CreateCounter<long>("sodalis.identity.password_changed_total", description: "Authenticated password change attempts.");

    public static readonly Counter<long> JwtAuthFailureTotal =
        Meter.CreateCounter<long>("sodalis.identity.jwt_auth_failure_total", description: "JWT bearer authentication failures.");
}
