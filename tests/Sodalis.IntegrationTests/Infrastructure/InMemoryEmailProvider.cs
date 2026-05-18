using System.Collections.Concurrent;
using Sodalis.Modules.Messaging.Domain;
using Sodalis.Modules.Messaging.Providers;

namespace Sodalis.IntegrationTests.Infrastructure;

/// <summary>
/// Test double for <see cref="IEmailProvider"/>. Captures every message that
/// would otherwise be sent over SMTP and exposes them for inspection.
/// Registered as a Singleton so tests across one fixture share the same store.
/// </summary>
public sealed class InMemoryEmailProvider : IEmailProvider
{
    private readonly ConcurrentBag<EmailMessage> _messages = new();

    public IReadOnlyCollection<EmailMessage> Messages => _messages;

    public Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        _messages.Add(message);
        return Task.CompletedTask;
    }

    public void Clear()
    {
        _messages.Clear();
    }

    /// <summary>
    /// Polls until a message matching <paramref name="predicate"/> is captured or
    /// <paramref name="timeout"/> elapses. Returns null on timeout. Useful because
    /// MessageSender dispatches email via Task.Run — there's a small async window
    /// between an endpoint returning and the email landing in this provider.
    /// </summary>
    public async Task<EmailMessage?> WaitForAsync(
        Func<EmailMessage, bool> predicate,
        TimeSpan? timeout = null,
        TimeSpan? pollInterval = null,
        CancellationToken ct = default)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(2));
        var poll = pollInterval ?? TimeSpan.FromMilliseconds(50);

        while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            var match = _messages.FirstOrDefault(predicate);
            if (match is not null)
                return match;
            await Task.Delay(poll, ct);
        }

        return null;
    }
}
