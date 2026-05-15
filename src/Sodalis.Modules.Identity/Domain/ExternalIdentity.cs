namespace Sodalis.Modules.Identity.Domain;

public sealed class ExternalIdentity
{
    public Guid PlayerId { get; init; }
    public Guid GameId { get; init; }
    public string ProviderId { get; init; } = "";
    public string ExternalId { get; init; } = "";
    public string? Metadata { get; set; }
    public DateTimeOffset LinkedAt { get; init; }
}
