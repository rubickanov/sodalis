using Sodalis.Modules.Messaging.Domain;

namespace Sodalis.Modules.Messaging.Providers;

public interface IEmailProvider
{
    Task SendAsync(EmailMessage message, CancellationToken ct);
}
