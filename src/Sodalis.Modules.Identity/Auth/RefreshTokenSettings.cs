namespace Sodalis.Modules.Identity.Auth;

public sealed class RefreshTokenSettings
{
    public const string SectionName = "Sodalis:Modules:Identity:RefreshToken";

    public int LifetimeDays { get; init; } = 30;
}
