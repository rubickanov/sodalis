namespace Sodalis.Modules.Messaging.Domain;

public sealed record EmailMessage(
    string ToAddress,
    string? ToName,
    string Subject,
    string HtmlBody,
    string TextBody,
    string FromAddress,
    string FromName,
    string? ReplyTo);
