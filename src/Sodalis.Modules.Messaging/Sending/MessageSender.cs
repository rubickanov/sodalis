using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sodalis.Modules.Messaging.Branding;
using Sodalis.Modules.Messaging.Domain;
using Sodalis.Modules.Messaging.Providers;

namespace Sodalis.Modules.Messaging.Sending;

internal sealed class MessageSender(
    BrandingResolver brandingResolver,
    EmailTemplateRenderer renderer,
    IServiceScopeFactory scopeFactory,
    ILogger<MessageSender> logger) : IMessageSender
{
    private const int MaxRetries = 3;
    private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromSeconds(2);

    public async Task SendEmailVerificationAsync(
        Guid gameId, string toEmail, string playerName, string verificationUrl, CancellationToken ct)
    {
        var branding = await brandingResolver.ResolveAsync(gameId, ct);
        var vars = BaseVars(branding, playerName);
        vars["verification_url"] = verificationUrl;

        var message = Build(branding, toEmail, playerName, TemplateKind.EmailVerification, vars);
        FireAndForget(message);
    }

    public async Task SendPasswordResetAsync(
        Guid gameId, string toEmail, string playerName, string resetUrl, TimeSpan expiresIn, CancellationToken ct)
    {
        var branding = await brandingResolver.ResolveAsync(gameId, ct);
        var vars = BaseVars(branding, playerName);
        vars["reset_url"] = resetUrl;
        vars["expires_in"] = HumanizeDuration(expiresIn);

        var message = Build(branding, toEmail, playerName, TemplateKind.PasswordReset, vars);
        FireAndForget(message);
    }

    public async Task SendPasswordChangedNotificationAsync(
        Guid gameId, string toEmail, string playerName, DateTimeOffset changedAt, CancellationToken ct)
    {
        var branding = await brandingResolver.ResolveAsync(gameId, ct);
        var vars = BaseVars(branding, playerName);
        vars["changed_at_utc"] = changedAt.UtcDateTime.ToString("u");

        var message = Build(branding, toEmail, playerName, TemplateKind.PasswordChanged, vars);
        FireAndForget(message);
    }

    private static Dictionary<string, string?> BaseVars(ResolvedBranding branding, string playerName) => new()
    {
        ["brand_name"] = branding.BrandName,
        ["player_name"] = playerName,
        ["logo_url"] = branding.LogoUrl,
        ["primary_color"] = branding.PrimaryColor,
        ["support_url"] = branding.SupportUrl,
        ["footer_text"] = branding.FooterText
    };

    private EmailMessage Build(
        ResolvedBranding branding,
        string toEmail,
        string playerName,
        TemplateKind kind,
        IReadOnlyDictionary<string, string?> vars)
    {
        var html = renderer.Render(kind, TemplateFormat.Html, vars);
        var text = renderer.Render(kind, TemplateFormat.Text, vars);
        var subject = renderer.GetSubject(kind, vars);

        return new EmailMessage(
            ToAddress: toEmail,
            ToName: playerName,
            Subject: subject,
            HtmlBody: html,
            TextBody: text,
            FromAddress: branding.FromAddress,
            FromName: branding.FromName,
            ReplyTo: branding.ReplyTo);
    }

    // TODO(durability): in-memory fire-and-forget. On host restart, any message
    // in flight is lost — password-reset emails included. Replace with an outbox
    // table: handler INSERTs into messaging.outbox in the same DB transaction as
    // the business write (token row), then a BackgroundService drains the outbox.
    // Survives restarts; gives at-least-once semantics.
    //
    // TODO(scale): no concurrency cap. A burst of N registers spawns N parallel
    // Task.Runs → N parallel SMTP connections. Gmail rate-limits at ~20/min; we
    // can self-DoS our own SMTP provider. Replace with a Channel<EmailMessage>
    // + a small worker pool (e.g. 4 workers) so concurrency is bounded.
    //
    // TODO(observability): permanent failures land in `LogError` and that's it.
    // No metrics (sent / failed / retried counters), no DLQ, no alert. Add
    // OpenTelemetry counters on each branch and write the message to a
    // messaging.failed_messages table on terminal failure so ops can replay/audit.
    private void FireAndForget(EmailMessage message)
    {
        // Capture a fresh scope for the background task — caller's scope will be
        // disposed long before we finish retrying.
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var provider = scope.ServiceProvider.GetRequiredService<IEmailProvider>();

                var delay = InitialRetryDelay;
                for (int attempt = 1; attempt <= MaxRetries; attempt++)
                {
                    try
                    {
                        await provider.SendAsync(message, CancellationToken.None);
                        return;
                    }
                    catch (Exception ex) when (attempt < MaxRetries)
                    {
                        logger.LogWarning(ex,
                            "Email delivery to {Recipient} failed (attempt {Attempt}/{Max}); retrying in {Delay}s.",
                            message.ToAddress, attempt, MaxRetries, delay.TotalSeconds);
                        await Task.Delay(delay, CancellationToken.None);
                        delay = TimeSpan.FromSeconds(delay.TotalSeconds * 2);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Email delivery to {Recipient} failed permanently after {Max} attempts.",
                    message.ToAddress, MaxRetries);
            }
        });
    }

    private static string HumanizeDuration(TimeSpan span)
    {
        if (span.TotalHours >= 1)
        {
            var h = (int)span.TotalHours;
            return $"{h} hour{(h == 1 ? "" : "s")}";
        }
        var m = (int)span.TotalMinutes;
        return $"{m} minute{(m == 1 ? "" : "s")}";
    }
}
