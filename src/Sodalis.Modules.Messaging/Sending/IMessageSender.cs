namespace Sodalis.Modules.Messaging.Sending;

/// <summary>
/// Public contract for sending transactional messages. Implementations are
/// fire-and-forget from the caller's perspective: the returned <see cref="Task"/>
/// completes once the message is queued for delivery, not when SMTP delivery
/// finishes. Failures during actual delivery are logged but not propagated.
/// </summary>
// TODO(api): method names end in `Async` but the returned Task does NOT track
// delivery — it completes once we've started the background send. This is a
// leaky abstraction. When durability lands (outbox), either rename to
// `Queue*Async` to be honest, or make the Task represent the durable enqueue
// (write to outbox) instead of the network send.
public interface IMessageSender
{
    Task SendEmailVerificationAsync(
        Guid gameId,
        string toEmail,
        string playerName,
        string verificationUrl,
        CancellationToken ct);

    Task SendPasswordResetAsync(
        Guid gameId,
        string toEmail,
        string playerName,
        string resetUrl,
        TimeSpan expiresIn,
        CancellationToken ct);

    Task SendPasswordChangedNotificationAsync(
        Guid gameId,
        string toEmail,
        string playerName,
        DateTimeOffset changedAt,
        CancellationToken ct);
}
