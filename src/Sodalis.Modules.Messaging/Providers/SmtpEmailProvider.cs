using System.Diagnostics;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using Sodalis.Modules.Messaging.Domain;
using Sodalis.Modules.Messaging.Settings;

namespace Sodalis.Modules.Messaging.Providers;

public sealed class SmtpEmailProvider(
    IOptions<MessagingSettings> options,
    ILogger<SmtpEmailProvider> logger) : IEmailProvider
{
    private readonly SmtpSettings _smtp = options.Value.Smtp;

    // TODO(scale): one TCP+TLS handshake per email. Connect/Auth/Send/Disconnect
    // costs ~200ms per message and burns one auth-attempt against rate-limited
    // providers (Gmail caps app-passwords). Hold a persistent SmtpClient per
    // worker, reconnect only on `ServiceNotAuthenticatedException` /
    // `SmtpProtocolException` — MailKit supports this out of the box.
    public async Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_smtp.Host))
        {
            // Production fail-fast happens in MessagingModule.RegisterServices.
            // Reaching here means Development/Test with no SMTP — silent dev no-op.
            logger.LogDebug("SMTP host is not configured; email to {Recipient} dropped.", message.ToAddress);
            return;
        }

        using var activity = MessagingTelemetry.ActivitySource.StartActivity(
            "messaging.smtp.send",
            ActivityKind.Client);
        activity?.SetTag("net.peer.name", _smtp.Host);
        activity?.SetTag("net.peer.port", _smtp.Port);
        activity?.SetTag("messaging.system", "smtp");

        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(message.FromName, message.FromAddress));
        mime.To.Add(new MailboxAddress(message.ToName ?? "", message.ToAddress));
        if (!string.IsNullOrWhiteSpace(message.ReplyTo))
        {
            mime.ReplyTo.Add(MailboxAddress.Parse(message.ReplyTo));
        }
        mime.Subject = message.Subject;
        mime.Body = new BodyBuilder
        {
            TextBody = message.TextBody,
            HtmlBody = message.HtmlBody
        }.ToMessageBody();

        using var client = new SmtpClient();

        var socketOptions = _smtp.UseStartTls
            ? SecureSocketOptions.StartTls
            : SecureSocketOptions.Auto;

        try
        {
            await client.ConnectAsync(_smtp.Host, _smtp.Port, socketOptions, ct);

            if (!string.IsNullOrWhiteSpace(_smtp.Username))
            {
                await client.AuthenticateAsync(_smtp.Username, _smtp.Password, ct);
            }

            await client.SendAsync(mime, ct);
            await client.DisconnectAsync(quit: true, ct);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.GetType().Name);
            throw;
        }
    }
}
