namespace Sodalis.Modules.Messaging.Settings;

public sealed class MessagingSettings
{
    public const string SectionName = "Sodalis:Modules:Messaging";

    public SmtpSettings Smtp { get; init; } = new();
    public BrandingDefaults DefaultBranding { get; init; } = new();
    public IReadOnlyList<BrandingSeed> GameBranding { get; init; } = [];
    public LinkBaseUrls LinkBaseUrls { get; init; } = new();
    public TokenLifetimes TokenLifetimes { get; init; } = new();
}

public sealed class SmtpSettings
{
    public string Host { get; init; } = "";
    public int Port { get; init; } = 587;
    public string Username { get; init; } = "";
    public string Password { get; init; } = "";
    public bool UseStartTls { get; init; } = true;
    public string FromAddress { get; init; } = "";
    public string FromName { get; init; } = "";
}

public sealed class BrandingDefaults
{
    public string BrandName { get; init; } = "Sodalis";
    public string? ReplyTo { get; init; }
    public string? LogoUrl { get; init; }
    public string PrimaryColor { get; init; } = "#2563eb";
    public string? SupportUrl { get; init; }
    public string FooterText { get; init; } = "";
}

public sealed class BrandingSeed
{
    public Guid GameId { get; init; }
    public string BrandName { get; init; } = "";
    public string? FromName { get; init; }
    public string? ReplyTo { get; init; }
    public string? LogoUrl { get; init; }
    public string? PrimaryColor { get; init; }
    public string? SupportUrl { get; init; }
    public string? FooterText { get; init; }
}

public sealed class LinkBaseUrls
{
    public string Verification { get; init; } = "";
    public string PasswordReset { get; init; } = "";
}

public sealed class TokenLifetimes
{
    public int VerificationHours { get; init; } = 24;
    public int PasswordResetMinutes { get; init; } = 60;
}
