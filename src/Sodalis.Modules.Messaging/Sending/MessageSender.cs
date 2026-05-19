using System.Diagnostics;
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
        FireAndForget(message, TemplateKind.EmailVerification, gameId);
    }

    public async Task SendPasswordResetAsync(
        Guid gameId, string toEmail, string playerName, string resetUrl, TimeSpan expiresIn, CancellationToken ct)
    {
        var branding = await brandingResolver.ResolveAsync(gameId, ct);
        var vars = BaseVars(branding, playerName);
        vars["reset_url"] = resetUrl;
        vars["expires_in"] = HumanizeDuration(expiresIn);

        var message = Build(branding, toEmail, playerName, TemplateKind.PasswordReset, vars);
        FireAndForget(message, TemplateKind.PasswordReset, gameId);
    }

    public async Task SendPasswordChangedNotificationAsync(
        Guid gameId, string toEmail, string playerName, DateTimeOffset changedAt, CancellationToken ct)
    {
        var branding = await brandingResolver.ResolveAsync(gameId, ct);
        var vars = BaseVars(branding, playerName);
        vars["changed_at_utc"] = changedAt.UtcDateTime.ToString("u");

        var message = Build(branding, toEmail, playerName, TemplateKind.PasswordChanged, vars);
        FireAndForget(message, TemplateKind.PasswordChanged, gameId);
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
    private void FireAndForget(EmailMessage message, TemplateKind kind, Guid gameId)
    {
        // Snapshot the caller's trace context now — by the time the background
        // task runs, Activity.Current will have been swapped out (request ended).
        // Passing the SpanContext explicitly lets OTel parent the background span
        // to the request that triggered it.
        var parentContext = Activity.Current?.Context ?? default;
        var kindTag = kind.ToString();

        _ = Task.Run(async () =>
        {
            using var activity = MessagingTelemetry.ActivitySource.StartActivity(
                "messaging.send_email",
                ActivityKind.Producer,
                parentContext);
            activity?.SetTag("sodalis.email.kind", kindTag);
            activity?.SetTag("sodalis.game.id", gameId);

            var sw = Stopwatch.StartNew();
            int finalAttempt = 0;
            string outcome = "failed";

            try
            {
                using var scope = scopeFactory.CreateScope();
                var provider = scope.ServiceProvider.GetRequiredService<IEmailProvider>();

                var delay = InitialRetryDelay;
                for (int attempt = 1; attempt <= MaxRetries; attempt++)
                {
                    finalAttempt = attempt;
                    try
                    {
                        await provider.SendAsync(message, CancellationToken.None);
                        outcome = "sent";
                        activity?.SetTag("sodalis.email.attempts", attempt);
                        return;
                    }
                    catch (Exception ex) when (attempt < MaxRetries)
                    {
                        logger.LogWarning(ex,
                            "Email delivery to {Recipient} failed (attempt {Attempt}/{Max}); retrying in {Delay}s.",
                            message.ToAddress, attempt, MaxRetries, delay.TotalSeconds);
                        activity?.AddEvent(new ActivityEvent("retry",
                            tags: new ActivityTagsCollection
                            {
                                { "attempt", attempt },
                                { "delay_seconds", delay.TotalSeconds }
                            }));
                        MessagingTelemetry.EmailSendTotal.Add(1,
                            new KeyValuePair<string, object?>("kind", kindTag),
                            new KeyValuePair<string, object?>("outcome", "retried"));
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
                activity?.SetStatus(ActivityStatusCode.Error, "permanent_failure");
                activity?.SetTag("sodalis.email.attempts", finalAttempt);
            }
            finally
            {
                sw.Stop();
                MessagingTelemetry.EmailSendDurationSeconds.Record(sw.Elapsed.TotalSeconds,
                    new KeyValuePair<string, object?>("kind", kindTag),
                    new KeyValuePair<string, object?>("outcome", outcome));
                MessagingTelemetry.EmailSendTotal.Add(1,
                    new KeyValuePair<string, object?>("kind", kindTag),
                    new KeyValuePair<string, object?>("outcome", outcome));

                if (outcome == "sent")
                {
                    logger.LogInformation(
                        "Email sent kind={Kind} recipient={Recipient} attempts={Attempts} durationMs={DurationMs}",
                        kindTag, message.ToAddress, finalAttempt, sw.ElapsedMilliseconds);
                }
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
