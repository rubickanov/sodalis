using System.Buffers.Text;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sodalis.Modules.Identity.AuthProviders;
using Sodalis.Modules.Identity.Domain;
using Sodalis.Modules.Identity.Persistence;
using Sodalis.Modules.Messaging.Sending;
using Sodalis.Modules.Messaging.Settings;

namespace Sodalis.Modules.Identity.Features.ForgotPassword;

public sealed class ForgotPasswordHandler(
    IdentityDbContext db,
    IMessageSender messageSender,
    IOptions<MessagingSettings> messagingOptions,
    ILogger<ForgotPasswordHandler> logger)
{
    private readonly MessagingSettings _messaging = messagingOptions.Value;

    public async Task HandleAsync(
        ForgotPasswordRequest request,
        Guid gameId,
        string? ipAddress,
        CancellationToken ct)
    {
        using var activity = IdentityTelemetry.ActivitySource.StartActivity("identity.password_reset_request");
        activity?.SetTag("sodalis.game.id", gameId);

        var email = EmailPasswordAuthProvider.NormalizeEmail(request.Email);

        var identity = await db.ExternalIdentities.FirstOrDefaultAsync(
            ei => ei.ProviderId == EmailPasswordAuthProvider.Id && ei.ExternalId == email,
            ct);

        // Anti-enumeration: silently exit if no email identity exists. Caller
        // sees the same 204 regardless.
        if (identity is null)
        {
            // Debug only — do NOT log the email here. The metric is the audit signal.
            logger.LogDebug("Password reset requested for unknown email (game {GameId})", gameId);
            IdentityTelemetry.PasswordResetRequestedTotal.Add(1,
                new KeyValuePair<string, object?>("outcome", "unknown_email"));
            // status stays Ok — observable failure here is a side channel.
            return;
        }

        var lifetime = TimeSpan.FromMinutes(_messaging.TokenLifetimes.PasswordResetMinutes);
        var now = DateTimeOffset.UtcNow;
        var raw = GenerateRawToken();
        var hash = HashToken(raw);

        db.PasswordResetTokens.Add(new PasswordResetToken
        {
            TokenHash = hash,
            PlayerId = identity.PlayerId,
            GameId = gameId,
            IssuedAt = now,
            ExpiresAt = now.Add(lifetime),
            IpAddress = ipAddress
        });
        await db.SaveChangesAsync(ct);

        var resetUrl = AppendToken(_messaging.LinkBaseUrls.PasswordReset, raw);
        await messageSender.SendPasswordResetAsync(
            gameId, email, playerName: email, resetUrl, lifetime, ct);

        activity?.SetTag("sodalis.player.id", identity.PlayerId);
        logger.LogInformation(
            "Password reset issued for player {PlayerId} (game {GameId})",
            identity.PlayerId, gameId);
        IdentityTelemetry.PasswordResetRequestedTotal.Add(1,
            new KeyValuePair<string, object?>("outcome", "issued"));
    }

    internal static string GenerateRawToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Base64Url.EncodeToString(bytes);
    }

    internal static string HashToken(string raw)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(raw), hash);
        return Convert.ToHexString(hash);
    }

    internal static string AppendToken(string baseUrl, string token)
    {
        var separator = baseUrl.Contains('?') ? '&' : '?';
        return $"{baseUrl}{separator}token={token}";
    }
}
