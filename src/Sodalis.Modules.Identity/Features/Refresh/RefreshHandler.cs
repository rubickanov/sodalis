using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sodalis.Modules.Identity.Auth;
using Sodalis.Modules.Identity.Features.Login;
using Sodalis.Modules.Identity.Persistence;

namespace Sodalis.Modules.Identity.Features.Refresh;

public sealed class RefreshHandler(
    IdentityDbContext db,
    RefreshTokenService refreshTokens,
    JwtIssuer jwtIssuer,
    ILogger<RefreshHandler> logger)
{
    public async Task<RefreshResult> HandleAsync(
        RefreshRequest request,
        Guid gameId,
        string? userAgent,
        string? ipAddress,
        CancellationToken ct)
    {
        using var activity = IdentityTelemetry.ActivitySource.StartActivity("identity.refresh");
        activity?.SetTag("sodalis.game.id", gameId);

        var rotation = await refreshTokens.ValidateAndRotateAsync(
            request.RefreshToken, gameId, userAgent, ipAddress, ct);

        if (!rotation.Success)
        {
            if (rotation.ReuseDetected)
            {
                logger.LogError(
                    "Refresh token reuse detected — entire session chain revoked. gameId={GameId}",
                    gameId);
                IdentityTelemetry.RefreshReuseDetectedTotal.Add(1);
                IdentityTelemetry.RefreshTotal.Add(1, new KeyValuePair<string, object?>("outcome", "reused"));
                activity?.SetStatus(ActivityStatusCode.Error, "session_compromised");
                return RefreshResult.SessionCompromised(rotation.Error!);
            }

            var reason = rotation.Error?.Contains("expired", StringComparison.OrdinalIgnoreCase) == true
                ? "expired"
                : "invalid";
            logger.LogInformation("Refresh failed: {Reason}", reason);
            IdentityTelemetry.RefreshTotal.Add(1, new KeyValuePair<string, object?>("outcome", reason));
            activity?.SetStatus(ActivityStatusCode.Error, reason);
            return RefreshResult.Failed(rotation.Error!);
        }

        var player = await db.Players
            .Include(p => p.ExternalIdentities)
            .FirstOrDefaultAsync(p => p.PlayerId == rotation.PlayerId, ct);

        if (player is null)
        {
            logger.LogWarning("Refresh failed: player {PlayerId} not found after rotation", rotation.PlayerId);
            IdentityTelemetry.RefreshTotal.Add(1, new KeyValuePair<string, object?>("outcome", "player_missing"));
            activity?.SetStatus(ActivityStatusCode.Error, "player_missing");
            return RefreshResult.Failed("Player not found.");
        }

        if (player.IsBanned)
        {
            // Revoke the just-rotated token so caller can't keep using it.
            await refreshTokens.RevokeAsync(rotation.NewRawToken!, gameId, ct);
            logger.LogWarning("Refresh rejected: player {PlayerId} is banned", player.PlayerId);
            IdentityTelemetry.RefreshTotal.Add(1, new KeyValuePair<string, object?>("outcome", "banned"));
            activity?.SetStatus(ActivityStatusCode.Error, "banned");
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

        activity?.SetTag("sodalis.player.id", player.PlayerId);

        logger.LogInformation("Refresh OK player={PlayerId}", player.PlayerId);
        IdentityTelemetry.RefreshTotal.Add(1, new KeyValuePair<string, object?>("outcome", "ok"));

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
