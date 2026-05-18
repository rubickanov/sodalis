namespace Sodalis.Modules.Tenancy.Domain;

public sealed class GameApiKey
{
    public string KeyHash { get; init; } = "";
    public Guid GameId { get; init; }
    public string Prefix { get; init; } = "";
    public string Name { get; init; } = "";
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? RevokedAt { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
}
