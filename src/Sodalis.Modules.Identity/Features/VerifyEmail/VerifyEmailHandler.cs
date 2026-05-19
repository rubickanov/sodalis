using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sodalis.Modules.Identity.AuthProviders;
using Sodalis.Modules.Identity.Persistence;

namespace Sodalis.Modules.Identity.Features.VerifyEmail;

public sealed class VerifyEmailHandler(
    IdentityDbContext db,
    ILogger<VerifyEmailHandler> logger)
{
    public async Task<VerifyEmailResult> HandleAsync(VerifyEmailRequest request, CancellationToken ct)
    {
        using var activity = IdentityTelemetry.ActivitySource.StartActivity("identity.verify_email");

        var hash = HashToken(request.Token);
        var now = DateTimeOffset.UtcNow;

        var token = await db.EmailVerificationTokens
            .FirstOrDefaultAsync(
                t => t.TokenHash == hash && t.UsedAt == null && t.ExpiresAt > now,
                ct);

        if (token is null)
        {
            logger.LogInformation("Email verification failed: invalid or expired token");
            IdentityTelemetry.EmailVerifiedTotal.Add(1,
                new KeyValuePair<string, object?>("outcome", "invalid_token"));
            activity?.SetStatus(ActivityStatusCode.Error, "invalid_token");
            return VerifyEmailResult.Failed("Invalid or expired token.");
        }

        var identity = await db.ExternalIdentities.FirstOrDefaultAsync(
            ei => ei.PlayerId == token.PlayerId && ei.ProviderId == EmailPasswordAuthProvider.Id,
            ct);

        if (identity?.Metadata is null)
        {
            logger.LogWarning("Email verification: token valid but identity missing for player {PlayerId}", token.PlayerId);
            IdentityTelemetry.EmailVerifiedTotal.Add(1,
                new KeyValuePair<string, object?>("outcome", "identity_missing"));
            activity?.SetStatus(ActivityStatusCode.Error, "identity_missing");
            return VerifyEmailResult.Failed("Invalid or expired token.");
        }

        var meta = JsonSerializer.Deserialize<EmailMetadata>(identity.Metadata);
        if (meta is null)
        {
            logger.LogWarning("Email verification: malformed metadata for player {PlayerId}", token.PlayerId);
            IdentityTelemetry.EmailVerifiedTotal.Add(1,
                new KeyValuePair<string, object?>("outcome", "metadata_corrupt"));
            activity?.SetStatus(ActivityStatusCode.Error, "metadata_corrupt");
            return VerifyEmailResult.Failed("Invalid or expired token.");
        }

        identity.Metadata = JsonSerializer.Serialize(meta with
        {
            EmailVerified = true,
            EmailVerifiedAt = now
        });

        token.UsedAt = now;

        await db.SaveChangesAsync(ct);

        activity?.SetTag("sodalis.player.id", token.PlayerId);
        logger.LogInformation("Email verified for player {PlayerId}", token.PlayerId);
        IdentityTelemetry.EmailVerifiedTotal.Add(1, new KeyValuePair<string, object?>("outcome", "ok"));

        return VerifyEmailResult.Ok();
    }

    internal static string HashToken(string raw)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(raw), hash);
        return Convert.ToHexString(hash);
    }
}

public sealed record VerifyEmailResult(bool Success, string? Error)
{
    public static VerifyEmailResult Ok() => new(true, null);
    public static VerifyEmailResult Failed(string error) => new(false, error);
}
