using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sodalis.Modules.Identity.Auth;
using Sodalis.Modules.Identity.AuthProviders;
using Sodalis.Modules.Identity.Domain;
using Sodalis.Modules.Identity.Features.Login;
using Sodalis.Modules.Identity.Persistence;

namespace Sodalis.Modules.Identity.Features.Register;

public sealed class RegisterHandler(
    IdentityDbContext db,
    PasswordHasher hasher,
    JwtIssuer jwtIssuer,
    RefreshTokenService refreshTokens)
{
    public async Task<RegisterResult> HandleAsync(
        RegisterRequest request,
        Guid gameId,
        string? userAgent,
        string? ipAddress,
        CancellationToken ct)
    {
        var email = EmailPasswordAuthProvider.NormalizeEmail(request.Email);

        var alreadyExists = await db.ExternalIdentities.AnyAsync(
            ei => ei.GameId == gameId
                  && ei.ProviderId == EmailPasswordAuthProvider.Id
                  && ei.ExternalId == email,
            ct);

        if (alreadyExists)
        {
            return RegisterResult.Failed("Email is already registered.");
        }

        var passwordHash = hasher.Hash(request.Password);
        var metadata = JsonSerializer.Serialize(new EmailMetadata(passwordHash, EmailVerified: false));

        var now = DateTimeOffset.UtcNow;
        var player = new Player
        {
            PlayerId = Guid.NewGuid(),
            GameId = gameId,
            CreatedAt = now,
            LastLoginAt = now,
            ExternalIdentities =
            [
                new ExternalIdentity
                {
                    GameId = gameId,
                    ProviderId = EmailPasswordAuthProvider.Id,
                    ExternalId = email,
                    LinkedAt = now,
                    Metadata = metadata
                }
            ]
        };

        db.Players.Add(player);
        await db.SaveChangesAsync(ct);

        var linkedProviders = new[] { EmailPasswordAuthProvider.Id };
        var accessToken = jwtIssuer.Issue(player.PlayerId, gameId, linkedProviders);
        var refresh = await refreshTokens.IssueAsync(player.PlayerId, gameId, userAgent, ipAddress, ct);

        var response = new LoginResponse(
            AccessToken: accessToken.Value,
            ExpiresIn: (int)(accessToken.ExpiresAt - now).TotalSeconds,
            RefreshToken: refresh.RawToken,
            RefreshTokenExpiresIn: (int)(refresh.ExpiresAt - now).TotalSeconds,
            TokenType: "Bearer",
            Player: new PlayerInfo(player.PlayerId, IsNew: true, linkedProviders));

        return RegisterResult.Ok(response);
    }
}

public sealed record RegisterResult(bool Success, LoginResponse? Response, string? Error)
{
    public static RegisterResult Ok(LoginResponse response) => new(true, response, null);
    public static RegisterResult Failed(string error) => new(false, null, error);
}
