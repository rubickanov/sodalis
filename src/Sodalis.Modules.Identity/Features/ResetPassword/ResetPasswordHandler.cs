using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sodalis.Modules.Identity.Auth;
using Sodalis.Modules.Identity.AuthProviders;
using Sodalis.Modules.Identity.Features.ForgotPassword;
using Sodalis.Modules.Identity.Persistence;
using Sodalis.Modules.Messaging.Sending;

namespace Sodalis.Modules.Identity.Features.ResetPassword;

public sealed class ResetPasswordHandler(
    IdentityDbContext db,
    PasswordHasher hasher,
    RefreshTokenService refreshTokens,
    IMessageSender messageSender,
    ILogger<ResetPasswordHandler> logger)
{
    public async Task<ResetPasswordResult> HandleAsync(
        ResetPasswordRequest request,
        Guid gameId,
        CancellationToken ct)
    {
        using var activity = IdentityTelemetry.ActivitySource.StartActivity("identity.password_reset_complete");
        activity?.SetTag("sodalis.game.id", gameId);

        var hash = ForgotPasswordHandler.HashToken(request.Token);
        var now = DateTimeOffset.UtcNow;

        var token = await db.PasswordResetTokens
            .FirstOrDefaultAsync(
                t => t.TokenHash == hash && t.UsedAt == null && t.ExpiresAt > now,
                ct);

        if (token is null)
        {
            logger.LogInformation("Password reset complete failed: invalid or expired token");
            IdentityTelemetry.PasswordResetCompletedTotal.Add(1,
                new KeyValuePair<string, object?>("outcome", "invalid_token"));
            activity?.SetStatus(ActivityStatusCode.Error, "invalid_token");
            return ResetPasswordResult.Failed("Invalid or expired token.");
        }

        var identity = await db.ExternalIdentities.FirstOrDefaultAsync(
            ei => ei.PlayerId == token.PlayerId && ei.ProviderId == EmailPasswordAuthProvider.Id,
            ct);

        if (identity?.Metadata is null)
        {
            logger.LogWarning("Password reset complete failed: identity missing for player {PlayerId}", token.PlayerId);
            IdentityTelemetry.PasswordResetCompletedTotal.Add(1,
                new KeyValuePair<string, object?>("outcome", "identity_missing"));
            activity?.SetStatus(ActivityStatusCode.Error, "identity_missing");
            return ResetPasswordResult.Failed("Invalid or expired token.");
        }

        var meta = JsonSerializer.Deserialize<EmailMetadata>(identity.Metadata);
        if (meta is null)
        {
            logger.LogWarning("Password reset complete failed: malformed metadata for player {PlayerId}", token.PlayerId);
            IdentityTelemetry.PasswordResetCompletedTotal.Add(1,
                new KeyValuePair<string, object?>("outcome", "metadata_corrupt"));
            activity?.SetStatus(ActivityStatusCode.Error, "metadata_corrupt");
            return ResetPasswordResult.Failed("Invalid or expired token.");
        }

        var newHash = hasher.Hash(request.NewPassword);
        identity.Metadata = JsonSerializer.Serialize(meta with { PasswordHash = newHash });

        token.UsedAt = now;

        await db.SaveChangesAsync(ct);

        // Kill all sessions — anyone who had the old password should not retain access.
        await refreshTokens.RevokeAllForPlayerAsync(token.PlayerId, gameId, ct);

        // Notify the user that their password just changed.
        await messageSender.SendPasswordChangedNotificationAsync(
            gameId, identity.ExternalId, identity.ExternalId, now, ct);

        activity?.SetTag("sodalis.player.id", token.PlayerId);
        logger.LogInformation("Password reset completed for player {PlayerId}", token.PlayerId);
        IdentityTelemetry.PasswordResetCompletedTotal.Add(1,
            new KeyValuePair<string, object?>("outcome", "ok"));

        return ResetPasswordResult.Ok();
    }
}

public sealed record ResetPasswordResult(bool Success, string? Error)
{
    public static ResetPasswordResult Ok() => new(true, null);
    public static ResetPasswordResult Failed(string error) => new(false, error);
}
