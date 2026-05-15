using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Sodalis.Modules.Identity.Auth;

public sealed class JwtIssuer(IOptions<JwtSettings> options)
{
    private readonly JwtSettings _settings = options.Value;
    private readonly JsonWebTokenHandler _handler = new();

    public IssuedToken Issue(
        Guid playerId,
        Guid gameId,
        IReadOnlyCollection<string> linkedProviders)
    {
        if (string.IsNullOrWhiteSpace(_settings.SigningKey))
            throw new InvalidOperationException("JWT SigningKey is not configured.");

        var keyBytes = Encoding.UTF8.GetBytes(_settings.SigningKey);
        if (keyBytes.Length < 32)
            throw new InvalidOperationException("JWT SigningKey must be at least 32 bytes (256 bits) for HS256.");

        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(_settings.AccessTokenLifetimeMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _settings.Issuer,
            Audience = _settings.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, playerId.ToString()),
                new Claim("gid", gameId.ToString()),
                new Claim("auth", string.Join(',', linkedProviders))
            ]),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(keyBytes),
                SecurityAlgorithms.HmacSha256)
        };

        var token = _handler.CreateToken(descriptor);
        return new IssuedToken(token, expires);
    }
}

public sealed record IssuedToken(string Value, DateTimeOffset ExpiresAt);
