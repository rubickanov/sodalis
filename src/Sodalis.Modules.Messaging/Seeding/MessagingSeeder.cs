using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sodalis.Modules.Messaging.Domain;
using Sodalis.Modules.Messaging.Persistence;
using Sodalis.Modules.Messaging.Settings;

namespace Sodalis.Modules.Messaging.Seeding;

public sealed class MessagingSeeder(
    MessagingDbContext db,
    IOptions<MessagingSettings> options,
    ILogger<MessagingSeeder> logger)
{
    public async Task SeedAsync(CancellationToken ct)
    {
        var brandings = options.Value.GameBranding;
        if (brandings.Count == 0) return;

        var now = DateTimeOffset.UtcNow;
        foreach (var seed in brandings)
        {
            if (seed.GameId == Guid.Empty || string.IsNullOrWhiteSpace(seed.BrandName))
            {
                logger.LogWarning("Skipping invalid BrandingSeed entry (GameId={GameId})", seed.GameId);
                continue;
            }

            var existing = await db.GameEmailBrandings.FirstOrDefaultAsync(b => b.GameId == seed.GameId, ct);
            if (existing is null)
            {
                db.GameEmailBrandings.Add(new GameEmailBranding
                {
                    GameId = seed.GameId,
                    BrandName = seed.BrandName,
                    FromName = seed.FromName,
                    ReplyTo = seed.ReplyTo,
                    LogoUrl = seed.LogoUrl,
                    PrimaryColor = seed.PrimaryColor,
                    SupportUrl = seed.SupportUrl,
                    FooterText = seed.FooterText,
                    UpdatedAt = now
                });
                logger.LogInformation("Seeded email branding for game {GameId} '{Name}'", seed.GameId, seed.BrandName);
            }
            else
            {
                // Upsert behavior: keep config as source of truth on every restart.
                existing.BrandName = seed.BrandName;
                existing.FromName = seed.FromName;
                existing.ReplyTo = seed.ReplyTo;
                existing.LogoUrl = seed.LogoUrl;
                existing.PrimaryColor = seed.PrimaryColor;
                existing.SupportUrl = seed.SupportUrl;
                existing.FooterText = seed.FooterText;
                existing.UpdatedAt = now;
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
