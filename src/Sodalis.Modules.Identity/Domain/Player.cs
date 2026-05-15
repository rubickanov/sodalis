namespace Sodalis.Modules.Identity.Domain;

public sealed class Player
{
    public Guid PlayerId { get; init; }
    public Guid GameId { get; init; }
    public bool IsBanned { get; set; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? LastLoginAt { get; set; }

    public List<ExternalIdentity> ExternalIdentities { get; init; } = [];
}
