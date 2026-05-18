using System.Text.Json;
using Microsoft.EntityFrameworkCore;
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
    IMessageSender messageSender)
{
    public async Task<ChangePasswordResult> HandleAsync(
        ChangePasswordRequest request,
        Guid playerId,
        Guid gameId,
        string? userAgent,
        string? ipAddress,
        CancellationToken ct)
    {
        var emailIdentity = await db.ExternalIdentities.FirstOrDefaultAsync(
            ei => ei.PlayerId == playerId
                  && ei.GameId == gameId
                  && ei.ProviderId == EmailPasswordAuthProvider.Id,
            ct);

        if (emailIdentity?.Metadata is null)
        {
            return ChangePasswordResult.Failed("No password set for this account.");
        }

        var meta = JsonSerializer.Deserialize<EmailMetadata>(emailIdentity.Metadata);
        if (meta?.PasswordHash is null || !hasher.Verify(request.CurrentPassword, meta.PasswordHash))
        {
            return ChangePasswordResult.Failed("Current password is incorrect.");
        }

        if (request.NewPassword == request.CurrentPassword)
        {
            return ChangePasswordResult.Failed("New password must differ from current.");
        }

        var player = await db.Players
            .Include(p => p.ExternalIdentities)
            .FirstOrDefaultAsync(p => p.PlayerId == playerId, ct);

        if (player is null)
        {
            return ChangePasswordResult.Failed("Player not found.");
        }

        if (player.IsBanned)
        {
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
