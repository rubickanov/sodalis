using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Shouldly;
using Sodalis.Modules.Messaging.Branding;
using Sodalis.Modules.Messaging.Domain;
using Sodalis.Modules.Messaging.Persistence;
using Sodalis.Modules.Messaging.Settings;

namespace Sodalis.Modules.Messaging.UnitTests.Branding;

public class BrandingResolverTests
{
    private static readonly Guid GameWithOverride = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid GameWithoutOverride = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task Resolve_UnknownGame_ReturnsDefaultsAndSmtpFrom()
    {
        await using var db = CreateDb();
        var resolver = new BrandingResolver(db, NewCache(), Settings());

        var result = await resolver.ResolveAsync(GameWithoutOverride, CancellationToken.None);

        result.BrandName.ShouldBe("DefaultBrand");
        result.FromAddress.ShouldBe("noreply@example.test");
        result.FromName.ShouldBe("Default From");
        result.PrimaryColor.ShouldBe("#000000");
        result.FooterText.ShouldBe("default footer");
        result.LogoUrl.ShouldBe("https://cdn.example.test/default.png");
        result.SupportUrl.ShouldBe("https://support.example.test");
        result.ReplyTo.ShouldBe("reply@example.test");
    }

    [Fact]
    public async Task Resolve_GameWithOverride_PrefersPerGameValues()
    {
        await using var db = CreateDb();
        db.GameEmailBrandings.Add(new GameEmailBranding
        {
            GameId = GameWithOverride,
            BrandName = "AcmeGame",
            FromName = "Acme",
            ReplyTo = "acme-reply@example.test",
            LogoUrl = "https://cdn.example.test/acme.png",
            PrimaryColor = "#ff00ff",
            SupportUrl = "https://acme.example.test/support",
            FooterText = "© Acme"
        });
        await db.SaveChangesAsync();

        var resolver = new BrandingResolver(db, NewCache(), Settings());
        var result = await resolver.ResolveAsync(GameWithOverride, CancellationToken.None);

        result.BrandName.ShouldBe("AcmeGame");
        result.FromName.ShouldBe("Acme");
        result.ReplyTo.ShouldBe("acme-reply@example.test");
        result.LogoUrl.ShouldBe("https://cdn.example.test/acme.png");
        result.PrimaryColor.ShouldBe("#ff00ff");
        result.SupportUrl.ShouldBe("https://acme.example.test/support");
        result.FooterText.ShouldBe("© Acme");
    }

    [Fact]
    public async Task Resolve_GameWithOverride_AlwaysUsesSmtpFromAddress()
    {
        await using var db = CreateDb();
        db.GameEmailBrandings.Add(new GameEmailBranding
        {
            GameId = GameWithOverride,
            BrandName = "AcmeGame"
        });
        await db.SaveChangesAsync();

        var resolver = new BrandingResolver(db, NewCache(), Settings());
        var result = await resolver.ResolveAsync(GameWithOverride, CancellationToken.None);

        result.FromAddress.ShouldBe("noreply@example.test");
    }

    [Fact]
    public async Task Resolve_GameWithOverride_NullFieldsFallBackToDefaults()
    {
        await using var db = CreateDb();
        db.GameEmailBrandings.Add(new GameEmailBranding
        {
            GameId = GameWithOverride,
            BrandName = "AcmeGame"
            // every other field left null
        });
        await db.SaveChangesAsync();

        var resolver = new BrandingResolver(db, NewCache(), Settings());
        var result = await resolver.ResolveAsync(GameWithOverride, CancellationToken.None);

        result.BrandName.ShouldBe("AcmeGame");
        result.ReplyTo.ShouldBe("reply@example.test");
        result.LogoUrl.ShouldBe("https://cdn.example.test/default.png");
        result.PrimaryColor.ShouldBe("#000000");
        result.SupportUrl.ShouldBe("https://support.example.test");
        result.FooterText.ShouldBe("default footer");
    }

    [Fact]
    public async Task Resolve_EmptyBrandName_FallsBackToDefault()
    {
        // perGame.BrandName is non-nullable string; empty is treated as "no override".
        await using var db = CreateDb();
        db.GameEmailBrandings.Add(new GameEmailBranding
        {
            GameId = GameWithOverride,
            BrandName = ""
        });
        await db.SaveChangesAsync();

        var resolver = new BrandingResolver(db, NewCache(), Settings());
        var result = await resolver.ResolveAsync(GameWithOverride, CancellationToken.None);

        result.BrandName.ShouldBe("DefaultBrand");
    }

    [Fact]
    public async Task Resolve_CachesResult_SecondCallDoesNotHitDb()
    {
        await using var db = CreateDb();
        db.GameEmailBrandings.Add(new GameEmailBranding
        {
            GameId = GameWithOverride,
            BrandName = "AcmeGame"
        });
        await db.SaveChangesAsync();

        var cache = NewCache();
        var resolver = new BrandingResolver(db, cache, Settings());

        var first = await resolver.ResolveAsync(GameWithOverride, CancellationToken.None);
        first.BrandName.ShouldBe("AcmeGame");

        // Mutate DB after the cache is warm — resolver should still return the
        // cached value, proving the second call doesn't go to the database.
        var row = await db.GameEmailBrandings.SingleAsync(b => b.GameId == GameWithOverride);
        row.BrandName = "ChangedAfterCache";
        await db.SaveChangesAsync();

        var second = await resolver.ResolveAsync(GameWithOverride, CancellationToken.None);
        second.BrandName.ShouldBe("AcmeGame");
    }

    [Fact]
    public async Task Resolve_UnknownGame_IsCachedToo()
    {
        await using var db = CreateDb();
        var cache = NewCache();
        var resolver = new BrandingResolver(db, cache, Settings());

        var first = await resolver.ResolveAsync(GameWithoutOverride, CancellationToken.None);
        first.BrandName.ShouldBe("DefaultBrand");

        // Insert a row after the negative cache entry is set.
        db.GameEmailBrandings.Add(new GameEmailBranding
        {
            GameId = GameWithoutOverride,
            BrandName = "LateOverride"
        });
        await db.SaveChangesAsync();

        var second = await resolver.ResolveAsync(GameWithoutOverride, CancellationToken.None);
        second.BrandName.ShouldBe("DefaultBrand");
    }

    private static MessagingDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<MessagingDbContext>()
            .UseInMemoryDatabase($"branding-tests-{Guid.NewGuid()}")
            .Options;
        return new MessagingDbContext(options);
    }

    private static IMemoryCache NewCache() => new MemoryCache(new MemoryCacheOptions());

    private static IOptions<MessagingSettings> Settings() => Options.Create(new MessagingSettings
    {
        Smtp = new SmtpSettings
        {
            Host = "smtp.example.test",
            FromAddress = "noreply@example.test",
            FromName = "Default From"
        },
        DefaultBranding = new BrandingDefaults
        {
            BrandName = "DefaultBrand",
            ReplyTo = "reply@example.test",
            LogoUrl = "https://cdn.example.test/default.png",
            PrimaryColor = "#000000",
            SupportUrl = "https://support.example.test",
            FooterText = "default footer"
        }
    });
}
