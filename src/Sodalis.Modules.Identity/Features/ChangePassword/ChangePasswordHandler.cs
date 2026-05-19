using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sodalis.Modules.Identity.Auth;
using Sodalis.Modules.Identity.AuthProviders;
using Sodalis.Modules.Identity.Features.Login;
using Sodalis.Modules.Identity.Persistence;
using Sodalis.Modules.Messaging.Sending;

namespace Sodalis.Modules.Identity.Features.ChangePassword;

public sealed class ChangePasswordHandler(
    IdentityDbContext db,
    PasswordHasher hasher,
    JwtIssuer jwtIssuer,
    RefreshTokenService refreshTokens,
    IMessageSender messageSender,
    ILogger<ChangePasswordHandler> logger)
{
    public async Task<ChangePasswordResult> HandleAsync(
        ChangePasswordRequest request,
        Guid playerId,
        Guid gameId,
        string? userAgent,
        string? ipAddress,
        CancellationToken ct)
    {
        using var activity = IdentityTelemetry.ActivitySource.StartActivity("identity.change_password");
        activity?.SetTag("sodalis.game.id", gameId);
        activity?.SetTag("sodalis.player.id", playerId);

        var emailIdentity = await db.ExternalIdentities.FirstOrDefaultAsync(
            ei => ei.PlayerId == playerId
                  && ei.GameId == gameId
                  && ei.ProviderId == EmailPasswordAuthProvider.Id,
            ct);

        if (emailIdentity?.Metadata is null)
        {
            logger.LogInformation("Change password failed: no password set for player {PlayerId}", playerId);
            IdentityTelemetry.PasswordChangedTotal.Add(1,
                new KeyValuePair<string, object?>("outcome", "failure"),
                new KeyValuePair<string, object?>("reason", "no_password"));
            activity?.SetStatus(ActivityStatusCode.Error, "no_password");
            return ChangePasswordResult.Failed("No password set for this account.");
        }

        var meta = JsonSerializer.Deserialize<EmailMetadata>(emailIdentity.Metadata);
        if (meta?.PasswordHash is null || !hasher.Verify(request.CurrentPassword, meta.PasswordHash))
        {
            logger.LogInformation("Change password failed: bad current password for player {PlayerId}", playerId);
            IdentityTelemetry.PasswordChangedTotal.Add(1,
                new KeyValuePair<string, object?>("outcome", "failure"),
                new KeyValuePair<string, object?>("reason", "bad_current_password"));
            activity?.SetStatus(ActivityStatusCode.Error, "bad_current_password");
            return ChangePasswordResult.Failed("Current password is incorrect.");
        }

        if (request.NewPassword == request.CurrentPassword)
        {
            logger.LogInformation("Change password failed: new password equals current for player {PlayerId}", playerId);
            IdentityTelemetry.PasswordChangedTotal.Add(1,
                new KeyValuePair<string, object?>("outcome", "failure"),
                new KeyValuePair<string, object?>("reason", "same_password"));
            activity?.SetStatus(ActivityStatusCode.Error, "same_password");
            return ChangePasswordResult.Failed("New password must differ from current.");
        }

        var player = await db.Players
            .Include(p => p.ExternalIdentities)
            .FirstOrDefaultAsync(p => p.PlayerId == playerId, ct);

        if (player is null)
        {
            logger.LogWarning("Change password failed: player {PlayerId} not found", playerId);
            IdentityTelemetry.PasswordChangedTotal.Add(1,
                new KeyValuePair<string, object?>("outcome", "failure"),
                new KeyValuePair<string, object?>("reason", "player_missing"));
            activity?.SetStatus(ActivityStatusCode.Error, "player_missing");
            return ChangePasswordResult.Failed("Player not found.");
        }

        if (player.IsBanned)
        {
            logger.LogWarning("Change password rejected: player {PlayerId} is banned", playerId);
            IdentityTelemetry.PasswordChangedTotal.Add(1,
                new KeyValuePair<string, object?>("outcome", "failure"),
                new KeyValuePair<string, object?>("reason", "banned"));
            activity?.SetStatus(ActivityStatusCode.Error, "banned");
            return ChangePasswordResult.Failed("Account is banned.");
        }

        var newHash = hasher.Hash(request.NewPassword);
        emailIdentity.Metadata = JsonSerializer.Serialize(meta with { PasswordHash = newHash });
        await db.SaveChangesAsync(ct);

        // Order matters: revoke-all BEFORE issuing the new refresh token, so the
        // sweep doesn't kill the token we just minted for the caller.
        await refreshTokens.RevokeAllForPlayerAsync(playerId, gameId, ct);

        var linkedProviders = player.ExternalIdentities.Select(ei => ei.ProviderId).ToList();
        var accessToken = jwtIssuer.Issue(playerId, gameId, linkedProviders);
        var refresh = await refreshTokens.IssueAsync(playerId, gameId, userAgent, ipAddress, ct);

        var now = DateTimeOffset.UtcNow;

        // Security notification — fire-and-forget; failure must not break the
        // password change.
        await messageSender.SendPasswordChangedNotificationAsync(
            gameId, emailIdentity.ExternalId, emailIdentity.ExternalId, now, ct);

        logger.LogInformation("Password changed for player {PlayerId}", playerId);
        IdentityTelemetry.PasswordChangedTotal.Add(1, new KeyValuePair<string, object?>("outcome", "success"));

        var response = new LoginResponse(
            AccessToken: accessToken.Value,
            ExpiresIn: (int)(accessToken.ExpiresAt - now).TotalSeconds,
            RefreshToken: refresh.RawToken,
            RefreshTokenExpiresIn: (int)(refresh.ExpiresAt - now).TotalSeconds,
            TokenType: "Bearer",
            Player: new PlayerInfo(playerId, IsNew: false, linkedProviders));

        return ChangePasswordResult.Ok(response);
    }
}

public sealed record ChangePasswordResult(bool Success, LoginResponse? Response, string? Error)
{
    public static ChangePasswordResult Ok(LoginResponse response) => new(true, response, null);
    public static ChangePasswordResult Failed(string error) => new(false, null, error);
}
