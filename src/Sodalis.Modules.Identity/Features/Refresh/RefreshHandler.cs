using Microsoft.EntityFrameworkCore;
using Sodalis.Modules.Identity.Auth;
using Sodalis.Modules.Identity.Features.Login;
using Sodalis.Modules.Identity.Persistence;

namespace Sodalis.Modules.Identity.Features.Refresh;

public sealed class RefreshHandler(
    IdentityDbContext db,
    RefreshTokenService refreshTokens,
    JwtIssuer jwtIssuer)
{
    public async Task<RefreshResult> HandleAsync(
        RefreshRequest request,
        Guid gameId,
        string? userAgent,
        string? ipAddress,
        CancellationToken ct)
    {
        var rotation = await refreshTokens.ValidateAndRotateAsync(
            request.RefreshToken, gameId, userAgent, ipAddress, ct);

        if (!rotation.Success)
        {
            return rotation.ReuseDetected
                ? RefreshResult.SessionCompromised(rotation.Error!)
                : RefreshResult.Failed(rotation.Error!);
        }

        var player = await db.Players
            .Include(p => p.ExternalIdentities)
            .FirstOrDefaultAsync(p => p.PlayerId == rotation.PlayerId, ct);

        if (player is null)
            return RefreshResult.Failed("Player not found.");

        if (player.IsBanned)
        {
            // Revoke the just-rotated token so caller can't keep using it.
            await refreshTokens.RevokeAsync(rotation.NewRawToken!, gameId, ct);
            return RefreshResult.Failed("Account is banned.");
        }

        var linkedProviders = player.ExternalIdentities.Select(ei => ei.ProviderId).ToList();
        var accessToken = jwtIssuer.Issue(player.PlayerId, gameId, linkedProviders);

        var now = DateTimeOffset.UtcNow;
        var response = new LoginResponse(
            AccessToken: accessToken.Value,
            ExpiresIn: (int)(accessToken.ExpiresAt - now).TotalSeconds,
            RefreshToken: rotation.NewRawToken!,
            RefreshTokenExpiresIn: (int)(rotation.NewExpiresAt - now).TotalSeconds,
            TokenType: "Bearer",
            Player: new PlayerInfo(player.PlayerId, IsNew: false, linkedProviders));

        return RefreshResult.Ok(response);
    }
}

public sealed record RefreshResult(
    bool Success,
    LoginResponse? Response,
    string? Error,
    bool Compromised)
{
    public static RefreshResult Ok(LoginResponse response) => new(true, response, null, false);
    public static RefreshResult Failed(string error) => new(false, null, error, false);
    public static RefreshResult SessionCompromised(string error) => new(false, null, error, true);
}
