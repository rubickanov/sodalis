namespace Sodalis.Modules.Tenancy.Seeding;

public sealed class TenancySeedSettings
{
    public const string SectionName = "Sodalis:Modules:Tenancy";

    public IReadOnlyList<SeedGame> SeedGames { get; init; } = [];
}

public sealed class SeedGame
{
    public Guid Id { get; init; }
    public string Name { get; init; } = "";
    public string ApiKey { get; init; } = "";
    public string KeyLabel { get; init; } = "default";
}
