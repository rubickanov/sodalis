using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Sodalis.Modules.Messaging.Domain;
using Sodalis.Modules.Messaging.Persistence;
using Sodalis.Modules.Messaging.Settings;

namespace Sodalis.Modules.Messaging.Branding;

public sealed class BrandingResolver(
    MessagingDbContext db,
    IMemoryCache cache,
    IOptions<MessagingSettings> options)
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);
    private readonly MessagingSettings _settings = options.Value;

    // TODO(footgun): cache is time-based only — no invalidation hook. When an
    // admin API for editing branding lands, branding edits will appear up to 60s
    // late. Expose an `Invalidate(Guid gameId)` method and call it from the
    // admin write-path. Or switch to a versioned key + `updated_at` check.
    public async Task<ResolvedBranding> ResolveAsync(Guid gameId, CancellationToken ct)
    {
        var cacheKey = $"branding:{gameId}";
        if (cache.TryGetValue<GameEmailBranding?>(cacheKey, out var cached))
        {
            return Merge(cached);
        }

        var row = await db.GameEmailBrandings
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.GameId == gameId, ct);

        cache.Set(cacheKey, row, CacheTtl);
        return Merge(row);
    }

    private ResolvedBranding Merge(GameEmailBranding? perGame)
    {
        var d = _settings.DefaultBranding;
        var smtp = _settings.Smtp;

        return new ResolvedBranding
        {
            BrandName = perGame?.BrandName is { Length: > 0 } b ? b : d.BrandName,
            FromAddress = smtp.FromAddress,
            FromName = perGame?.FromName ?? smtp.FromName,
            ReplyTo = perGame?.ReplyTo ?? d.ReplyTo,
            LogoUrl = perGame?.LogoUrl ?? d.LogoUrl,
            PrimaryColor = perGame?.PrimaryColor ?? d.PrimaryColor,
            SupportUrl = perGame?.SupportUrl ?? d.SupportUrl,
            FooterText = perGame?.FooterText ?? d.FooterText
        };
    }
}

public sealed class ResolvedBranding
{
    public required string BrandName { get; init; }
    public required string FromAddress { get; init; }
    public required string FromName { get; init; }
    public string? ReplyTo { get; init; }
    public string? LogoUrl { get; init; }
    public required string PrimaryColor { get; init; }
    public string? SupportUrl { get; init; }
    public required string FooterText { get; init; }
}
