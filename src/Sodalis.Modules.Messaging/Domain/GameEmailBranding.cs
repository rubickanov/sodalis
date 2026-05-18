namespace Sodalis.Modules.Messaging.Domain;

public sealed class GameEmailBranding
{
    public Guid GameId { get; init; }
    public string BrandName { get; set; } = "";
    public string? FromName { get; set; }
    public string? ReplyTo { get; set; }
    public string? LogoUrl { get; set; }
    public string? PrimaryColor { get; set; }
    public string? SupportUrl { get; set; }
    public string? FooterText { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
