namespace Sodalis.Modules.Identity.Domain;

public sealed class EmailVerificationToken
{
    public string TokenHash { get; init; } = "";
    public Guid PlayerId { get; init; }
    public Guid GameId { get; init; }
    public string Email { get; init; } = "";
    public DateTimeOffset IssuedAt { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public DateTimeOffset? UsedAt { get; set; }
}
