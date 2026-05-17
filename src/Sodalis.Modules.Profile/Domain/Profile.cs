namespace Sodalis.Modules.Profile.Domain;

public sealed class Profile
{
    public Guid PlayerId { get; init; }
    public Guid GameId { get; init; }
    public string DisplayName { get; set; } = "";
    public string AvatarUrl { get; set; } = "";
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; set; }
}
