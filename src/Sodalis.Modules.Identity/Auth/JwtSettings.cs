namespace Sodalis.Modules.Identity.Auth;

public sealed class JwtSettings
{
    public const string SectionName = "Sodalis:Modules:Identity:Jwt";

    public string Issuer { get; init; } = "sodalis";
    public string Audience { get; init; } = "sodalis";
    public string SigningKey { get; init; } = "";
    public int AccessTokenLifetimeMinutes { get; init; } = 15;
}
