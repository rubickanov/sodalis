namespace Sodalis.Modules.Tenancy.Domain;

public sealed class Game
{
    public Guid GameId { get; init; }
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; init; }

    public List<GameApiKey> ApiKeys { get; init; } = [];
}
