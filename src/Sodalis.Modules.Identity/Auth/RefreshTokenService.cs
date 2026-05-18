using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sodalis.Modules.Identity.Domain;
using Sodalis.Modules.Identity.Persistence;

namespace Sodalis.Modules.Identity.Auth;

public sealed class RefreshTokenService(
    IdentityDbContext db,
    IOptions<RefreshTokenSettings> options)
{
    private readonly RefreshTokenSettings _settings = options.Value;

    public async Task<IssuedRefreshToken> IssueAsync(
        Guid playerId,
        Guid gameId,
        string? userAgent,
        string? ipAddress,
        CancellationToken ct)
    {
        var raw = GenerateRawToken();
        var hash = HashToken(raw);
        var now = DateTimeOffset.UtcNow;
        var expires = now.AddDays(_settings.LifetimeDays);

        db.RefreshTokens.Add(new RefreshToken
        {
            TokenHash = hash,
            PlayerId = playerId,
            GameId = gameId,
            IssuedAt = now,
            ExpiresAt = expires,
            UserAgent = userAgent,
            IpAddress = ipAddress
        });
        await db.SaveChangesAsync(ct);

        return new IssuedRefreshToken(raw, expires);
    }

    public async Task<RotateResult> ValidateAndRotateAsync(
        string rawToken,
        Guid gameId,
        string? userAgent,
        string? ipAddress,
        CancellationToken ct)
    {
        var hash = HashToken(rawToken);
        var now = DateTimeOffset.UtcNow;
        var newRaw = GenerateRawToken();
        var newHash = HashToken(newRaw);
        var newExpires = now.AddDays(_settings.LifetimeDays);

        // Atomic claim: mark this token as rotated only if it's still pristine.
        // The WHERE predicate is the lock — Postgres serializes concurrent UPDATEs
        // on the same row, so exactly one caller sees rowsAffected == 1.
        // Without this, two concurrent refreshes could both succeed and reuse
        // detection would be silently bypassed.
        var rowsAffected = await db.RefreshTokens
            .Where(t => t.TokenHash == hash
                        && t.GameId == gameId
                        && t.RevokedAt == null
                        && t.ReplacedByHash == null
                        && t.ExpiresAt > now)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.RevokedAt, now)
                .SetProperty(t => t.ReplacedByHash, newHash)
                .SetProperty(t => t.LastUsedAt, now), ct);

        if (rowsAffected == 0)
        {
            // Re-read to disambiguate why we didn't claim the row.
            var existing = await db.RefreshTokens
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.TokenHash == hash && t.GameId == gameId, ct);

            if (existing is null)
            {
                return RotateResult.Invalid("Unknown refresh token.");
            }

            if (existing.ReplacedByHash is not null || existing.RevokedAt is not null)
            {
                await RevokeChainAsync(existing, ct);
                return RotateResult.Reused("Refresh token reuse detected; session revoked.");
            }

            return RotateResult.Invalid("Refresh token expired.");
        }

        // We won the race; fetch PlayerId to attach the new token.
        // FirstOrDefault (not First) — a future expired-token cleanup job could
        // race in and delete the row between our UPDATE and this SELECT.
        var playerId = await db.RefreshTokens
            .AsNoTracking()
            .Where(t => t.TokenHash == hash)
            .Select(t => (Guid?)t.PlayerId)
            .FirstOrDefaultAsync(ct);

        if (playerId is null)
        {
            return RotateResult.Invalid("Refresh token vanished between rotation and read.");
        }

        db.RefreshTokens.Add(new RefreshToken
        {
            TokenHash = newHash,
            PlayerId = playerId.Value,
            GameId = gameId,
            IssuedAt = now,
            ExpiresAt = newExpires,
            UserAgent = userAgent,
            IpAddress = ipAddress
        });

        await db.SaveChangesAsync(ct);
        return RotateResult.Ok(playerId.Value, newRaw, newExpires);
    }

    public async Task<bool> RevokeAsync(string rawToken, Guid gameId, CancellationToken ct)
    {
        var hash = HashToken(rawToken);
        var row = await db.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == hash && t.GameId == gameId, ct);

        if (row is null || row.RevokedAt is not null)
        {
            return false;
        }

        row.RevokedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<int> RevokeAllForPlayerAsync(Guid playerId, Guid gameId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        return await db.RefreshTokens
            .Where(t => t.PlayerId == playerId && t.GameId == gameId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);
    }

    private async Task RevokeChainAsync(RefreshToken start, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var current = start;
        while (current is not null)
        {
            current.RevokedAt ??= now;
            if (current.ReplacedByHash is null) break;
            current = await db.RefreshTokens
                .FirstOrDefaultAsync(t => t.TokenHash == current.ReplacedByHash, ct);
        }

        await db.SaveChangesAsync(ct);
    }

    private static string GenerateRawToken()
    {
        Span<byte> bytes = stackalloc byte[64];
        RandomNumberGenerator.Fill(bytes);
        return Base64Url.EncodeToString(bytes);
    }

    private static string HashToken(string raw)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(raw), hash);
        return Convert.ToHexString(hash);
    }
}

public sealed record IssuedRefreshToken(string RawToken, DateTimeOffset ExpiresAt);

public sealed record RotateResult(
    bool Success,
    bool ReuseDetected,
    Guid PlayerId,
    string? NewRawToken,
    DateTimeOffset NewExpiresAt,
    string? Error)
{
    public static RotateResult Ok(Guid playerId, string newRaw, DateTimeOffset newExpires) =>
        new(true, false, playerId, newRaw, newExpires, null);

    public static RotateResult Invalid(string error) =>
        new(false, false, Guid.Empty, null, default, error);

    public static RotateResult Reused(string error) =>
        new(false, true, Guid.Empty, null, default, error);
}
