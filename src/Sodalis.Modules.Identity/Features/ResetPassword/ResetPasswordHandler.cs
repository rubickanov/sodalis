using System.Text.Json;
using Microsoft.EntityFrameworkCore;
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
    IMessageSender messageSender)
{
    public async Task<ResetPasswordResult> HandleAsync(
        ResetPasswordRequest request,
        Guid gameId,
        CancellationToken ct)
    {
        var hash = ForgotPasswordHandler.HashToken(request.Token);
        var now = DateTimeOffset.UtcNow;

        var token = await db.PasswordResetTokens
            .FirstOrDefaultAsync(
                t => t.TokenHash == hash && t.UsedAt == null && t.ExpiresAt > now,
                ct);

        if (token is null)
        {
            return ResetPasswordResult.Failed("Invalid or expired token.");
        }

        var identity = await db.ExternalIdentities.FirstOrDefaultAsync(
            ei => ei.PlayerId == token.PlayerId && ei.ProviderId == EmailPasswordAuthProvider.Id,
            ct);

        if (identity?.Metadata is null)
        {
            return ResetPasswordResult.Failed("Invalid or expired token.");
        }

        var meta = JsonSerializer.Deserialize<EmailMetadata>(identity.Metadata);
        if (meta is null)
        {
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

        return ResetPasswordResult.Ok();
    }
}

public sealed record ResetPasswordResult(bool Success, string? Error)
{
    public static ResetPasswordResult Ok() => new(true, null);
    public static ResetPasswordResult Failed(string error) => new(false, error);
}
