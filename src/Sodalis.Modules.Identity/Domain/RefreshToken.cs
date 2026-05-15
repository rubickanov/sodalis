namespace Sodalis.Modules.Identity.Domain;

public sealed class RefreshToken
{
    public string TokenHash { get; init; } = "";
    public Guid PlayerId { get; init; }
    public Guid GameId { get; init; }
    public DateTimeOffset IssuedAt { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public DateTimeOffset? LastUsedAt { get; set; }
    public string? UserAgent { get; init; }
    public string? IpAddress { get; init; }
    public DateTimeOffset? RevokedAt { get; set; }
    public string? ReplacedByHash { get; set; }
}
