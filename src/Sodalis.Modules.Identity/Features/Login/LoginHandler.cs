using Microsoft.EntityFrameworkCore;
using Sodalis.Modules.Identity.Auth;
using Sodalis.Modules.Identity.AuthProviders;
using Sodalis.Modules.Identity.Domain;
using Sodalis.Modules.Identity.Persistence;

namespace Sodalis.Modules.Identity.Features.Login;

public sealed class LoginHandler(
    IdentityDbContext db,
    IEnumerable<IAuthProvider> providers,
    JwtIssuer jwtIssuer,
    RefreshTokenService refreshTokens)
{
    public async Task<LoginResult> HandleAsync(
        LoginRequest request,
        Guid gameId,
        string? userAgent,
        string? ipAddress,
        CancellationToken ct)
    {
        var provider = providers.FirstOrDefault(p => p.ProviderId == request.Provider);
        if (provider is null)
        {
            return LoginResult.Failed($"Unknown auth provider '{request.Provider}'.");
        }

        var auth = await provider.AuthenticateAsync(
            new AuthRequest(request.Provider, request.Payload, gameId), ct);

        if (!auth.Success || auth.ExternalId is null)
        {
            return LoginResult.Failed(auth.FailureReason ?? "Authentication failed.");
        }

        var existing = await db.ExternalIdentities
            .FirstOrDefaultAsync(
                ei => ei.GameId == gameId
                      && ei.ProviderId == request.Provider
                      && ei.ExternalId == auth.ExternalId,
                ct);

        Player player;
        bool isNew;

        if (existing is not null)
        {
            player = await db.Players
                .Include(p => p.ExternalIdentities)
                .FirstAsync(p => p.PlayerId == existing.PlayerId, ct);

            // Reject before touching LastLoginAt or persisting anything —
            // otherwise the audit log shows a successful login for a banned player.
            if (player.IsBanned)
            {
                return LoginResult.Failed("Account is banned.");
            }

            player.LastLoginAt = DateTimeOffset.UtcNow;
            isNew = false;
        }
        else
        {
            var now = DateTimeOffset.UtcNow;
            player = new Player
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
                        ProviderId = request.Provider,
                        ExternalId = auth.ExternalId,
                        LinkedAt = now
                    }
                ]
            };
            db.Players.Add(player);
            isNew = true;
        }

        await db.SaveChangesAsync(ct);

        var linkedProviders = player.ExternalIdentities.Select(ei => ei.ProviderId).ToList();
        var accessToken = jwtIssuer.Issue(player.PlayerId, gameId, linkedProviders);
        var refresh = await refreshTokens.IssueAsync(player.PlayerId, gameId, userAgent, ipAddress, ct);

        var now2 = DateTimeOffset.UtcNow;
        var response = new LoginResponse(
            AccessToken: accessToken.Value,
            ExpiresIn: (int)(accessToken.ExpiresAt - now2).TotalSeconds,
            RefreshToken: refresh.RawToken,
            RefreshTokenExpiresIn: (int)(refresh.ExpiresAt - now2).TotalSeconds,
            TokenType: "Bearer",
            Player: new PlayerInfo(player.PlayerId, isNew, linkedProviders));

        return LoginResult.Ok(response);
    }
}

public sealed record LoginResult(bool Success, LoginResponse? Response, string? Error)
{
    public static LoginResult Ok(LoginResponse response) => new(true, response, null);
    public static LoginResult Failed(string error) => new(false, null, error);
}
