using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sodalis.Modules.Tenancy.ApiKeys;
using Sodalis.Modules.Tenancy.Domain;
using Sodalis.Modules.Tenancy.Persistence;

namespace Sodalis.Modules.Tenancy.Seeding;

public sealed class TenancySeeder(
    TenancyDbContext db,
    IOptions<TenancySeedSettings> options,
    ILogger<TenancySeeder> logger)
{
    public async Task SeedAsync(CancellationToken ct)
    {
        var seedGames = options.Value.SeedGames;
        if (seedGames.Count == 0)
        {
            return;
        }

        foreach (var seed in seedGames)
        {
            if (seed.Id == Guid.Empty || string.IsNullOrWhiteSpace(seed.ApiKey))
            {
                logger.LogWarning("Skipping invalid SeedGame entry (Id={Id}, Name={Name})", seed.Id, seed.Name);
                continue;
            }

            var existingGame = await db.Games.FirstOrDefaultAsync(g => g.GameId == seed.Id, ct);
            if (existingGame is null)
            {
                db.Games.Add(new Game
                {
                    GameId = seed.Id,
                    Name = seed.Name,
                    IsActive = true,
                    CreatedAt = DateTimeOffset.UtcNow
                });
                logger.LogInformation("Seeded game {GameId} '{Name}'", seed.Id, seed.Name);
            }

            var keyHash = ApiKeyHasher.Hash(seed.ApiKey);
            var existingKey = await db.GameApiKeys.FirstOrDefaultAsync(k => k.KeyHash == keyHash, ct);
            if (existingKey is null)
            {
                db.GameApiKeys.Add(new GameApiKey
                {
                    KeyHash = keyHash,
                    GameId = seed.Id,
                    Prefix = ApiKeyHasher.Prefix(seed.ApiKey),
                    Name = seed.KeyLabel,
                    CreatedAt = DateTimeOffset.UtcNow
                });
                logger.LogInformation("Seeded API key for game {GameId} (prefix={Prefix})",
                    seed.Id, ApiKeyHasher.Prefix(seed.ApiKey));
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
