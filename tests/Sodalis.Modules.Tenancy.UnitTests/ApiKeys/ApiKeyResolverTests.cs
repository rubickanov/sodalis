using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Shouldly;
using Sodalis.Modules.Tenancy.ApiKeys;
using Sodalis.Modules.Tenancy.Domain;
using Sodalis.Modules.Tenancy.Persistence;

namespace Sodalis.Modules.Tenancy.UnitTests.ApiKeys;

public class ApiKeyResolverTests
{
    private static readonly Guid GameId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private const string RawKey = "sodalis_test_known_key_value_abcdefghijklmn";

    [Fact]
    public async Task ResolveGameId_ValidKey_ReturnsGameId()
    {
        await using var db = CreateDb();
        SeedActiveGameWithKey(db, GameId, RawKey);
        await db.SaveChangesAsync();

        var resolver = new ApiKeyResolver(db, NewCache());
        var result = await resolver.ResolveGameIdAsync(RawKey, CancellationToken.None);

        result.ShouldBe(GameId);
    }

    [Fact]
    public async Task ResolveGameId_UnknownKey_ReturnsNull()
    {
        await using var db = CreateDb();
        SeedActiveGameWithKey(db, GameId, RawKey);
        await db.SaveChangesAsync();

        var resolver = new ApiKeyResolver(db, NewCache());
        var result = await resolver.ResolveGameIdAsync("sodalis_test_other_unknown_value", CancellationToken.None);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task ResolveGameId_RevokedKey_ReturnsNull()
    {
        await using var db = CreateDb();
        var game = new Game { GameId = GameId, Name = "G", IsActive = true };
        var key = new GameApiKey
        {
            KeyHash = ApiKeyHasher.Hash(RawKey),
            GameId = GameId,
            Prefix = ApiKeyHasher.Prefix(RawKey),
            Name = "default",
            CreatedAt = DateTimeOffset.UtcNow,
            RevokedAt = DateTimeOffset.UtcNow
        };
        db.Games.Add(game);
        db.GameApiKeys.Add(key);
        await db.SaveChangesAsync();

        var resolver = new ApiKeyResolver(db, NewCache());
        var result = await resolver.ResolveGameIdAsync(RawKey, CancellationToken.None);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task ResolveGameId_InactiveGame_ReturnsNull()
    {
        await using var db = CreateDb();
        var game = new Game { GameId = GameId, Name = "G", IsActive = false };
        var key = new GameApiKey
        {
            KeyHash = ApiKeyHasher.Hash(RawKey),
            GameId = GameId,
            Prefix = ApiKeyHasher.Prefix(RawKey),
            Name = "default",
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Games.Add(game);
        db.GameApiKeys.Add(key);
        await db.SaveChangesAsync();

        var resolver = new ApiKeyResolver(db, NewCache());
        var result = await resolver.ResolveGameIdAsync(RawKey, CancellationToken.None);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task ResolveGameId_CachesPositiveResult()
    {
        await using var db = CreateDb();
        SeedActiveGameWithKey(db, GameId, RawKey);
        await db.SaveChangesAsync();

        var cache = NewCache();
        var resolver = new ApiKeyResolver(db, cache);

        var first = await resolver.ResolveGameIdAsync(RawKey, CancellationToken.None);
        first.ShouldBe(GameId);

        // Revoke the key directly in DB — cached resolver should still say it's valid.
        var key = await db.GameApiKeys.SingleAsync();
        key.RevokedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        var second = await resolver.ResolveGameIdAsync(RawKey, CancellationToken.None);
        second.ShouldBe(GameId);
    }

    [Fact]
    public async Task ResolveGameId_CachesNegativeResult()
    {
        await using var db = CreateDb();

        var cache = NewCache();
        var resolver = new ApiKeyResolver(db, cache);

        var first = await resolver.ResolveGameIdAsync(RawKey, CancellationToken.None);
        first.ShouldBeNull();

        // Seed the key AFTER the negative cache entry — cached resolver should still say null.
        SeedActiveGameWithKey(db, GameId, RawKey);
        await db.SaveChangesAsync();

        var second = await resolver.ResolveGameIdAsync(RawKey, CancellationToken.None);
        second.ShouldBeNull();
    }

    // Touch_UpdatesLastUsedAt is covered by the integration test suite — it uses
    // ExecuteUpdateAsync, which EFCore.InMemory does not implement. Verifying it
    // here would require SQLite-in-memory or a real Postgres, both of which the
    // integration tests already provide.

    private static TenancyDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<TenancyDbContext>()
            .UseInMemoryDatabase($"apikey-tests-{Guid.NewGuid()}")
            .Options;
        return new TenancyDbContext(options);
    }

    private static IMemoryCache NewCache() => new MemoryCache(new MemoryCacheOptions());

    private static void SeedActiveGameWithKey(TenancyDbContext db, Guid gameId, string rawKey)
    {
        db.Games.Add(new Game { GameId = gameId, Name = "G", IsActive = true });
        db.GameApiKeys.Add(new GameApiKey
        {
            KeyHash = ApiKeyHasher.Hash(rawKey),
            GameId = gameId,
            Prefix = ApiKeyHasher.Prefix(rawKey),
            Name = "default",
            CreatedAt = DateTimeOffset.UtcNow
        });
    }
}
