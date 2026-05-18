using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Sodalis.Modules.Tenancy.Persistence;

namespace Sodalis.Modules.Tenancy.ApiKeys;

public sealed class ApiKeyResolver(TenancyDbContext db, IMemoryCache cache)
{
    // Short TTL because revocations need to take effect quickly.
    // Trade-off: longer TTL = fewer DB hits but staler revocations.
    // 60s is the same order as JWT refresh intervals, acceptable.
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    public async Task<Guid?> ResolveGameIdAsync(string rawKey, CancellationToken ct)
    {
        var hash = ApiKeyHasher.Hash(rawKey);
        var cacheKey = $"apikey:{hash}";

        if (cache.TryGetValue<Guid?>(cacheKey, out var cached))
            return cached;

        var row = await db.GameApiKeys
            .AsNoTracking()
            .Where(k => k.KeyHash == hash && k.RevokedAt == null)
            .Join(db.Games.AsNoTracking().Where(g => g.IsActive),
                k => k.GameId,
                g => g.GameId,
                (k, g) => (Guid?)g.GameId)
            .FirstOrDefaultAsync(ct);

        cache.Set(cacheKey, row, CacheTtl);
        return row;
    }

    public Task TouchAsync(string rawKey, CancellationToken ct)
    {
        var hash = ApiKeyHasher.Hash(rawKey);
        var now = DateTimeOffset.UtcNow;

        // Fire-and-forget: don't block the request on a non-critical timestamp update.
        // We accept rare lost writes under contention.
        return db.GameApiKeys
            .Where(k => k.KeyHash == hash)
            .ExecuteUpdateAsync(s => s.SetProperty(k => k.LastUsedAt, now), ct);
    }
}
