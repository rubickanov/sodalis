using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sodalis.Modules.Messaging.Providers;
using Sodalis.Modules.Tenancy.ApiKeys;
using Testcontainers.PostgreSql;

namespace Sodalis.IntegrationTests.Infrastructure;

public sealed class SodalisFixture : IAsyncLifetime
{
    public Guid GameAId { get; } = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public Guid GameBId { get; } = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public string GameAKey { get; } = "sodalis_test_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    public string GameBKey { get; } = "sodalis_test_bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    public string GameABrand { get; } = "Test Game A";
    public string GameBBrand { get; } = "Test Game B";

    public string VerificationLinkBase { get; } = "http://test.local/verify-email";
    public string PasswordResetLinkBase { get; } = "http://test.local/reset-password";

    public InMemoryEmailProvider Emails { get; } = new();

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("sodalis_test")
        .WithUsername("sodalis")
        .WithPassword("sodalis")
        .Build();

    public WebApplicationFactory<Program> App { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        App = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Test");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Sodalis"] = _postgres.GetConnectionString(),

                    ["Sodalis:Modules:Tenancy:Enabled"] = "true",
                    ["Sodalis:Modules:Tenancy:SeedGames:0:Id"] = GameAId.ToString(),
                    ["Sodalis:Modules:Tenancy:SeedGames:0:Name"] = "Test Game A",
                    ["Sodalis:Modules:Tenancy:SeedGames:0:ApiKey"] = GameAKey,
                    ["Sodalis:Modules:Tenancy:SeedGames:0:KeyLabel"] = "default",
                    ["Sodalis:Modules:Tenancy:SeedGames:1:Id"] = GameBId.ToString(),
                    ["Sodalis:Modules:Tenancy:SeedGames:1:Name"] = "Test Game B",
                    ["Sodalis:Modules:Tenancy:SeedGames:1:ApiKey"] = GameBKey,
                    ["Sodalis:Modules:Tenancy:SeedGames:1:KeyLabel"] = "default",

                    ["Sodalis:Modules:Messaging:Enabled"] = "true",
                    ["Sodalis:Modules:Messaging:Smtp:Host"] = "",
                    ["Sodalis:Modules:Messaging:Smtp:FromAddress"] = "test@sodalis.test",
                    ["Sodalis:Modules:Messaging:Smtp:FromName"] = "Sodalis Test",
                    ["Sodalis:Modules:Messaging:DefaultBranding:BrandName"] = "Sodalis Default",
                    ["Sodalis:Modules:Messaging:DefaultBranding:PrimaryColor"] = "#2563eb",
                    ["Sodalis:Modules:Messaging:DefaultBranding:FooterText"] = "Sodalis Test Footer",
                    ["Sodalis:Modules:Messaging:GameBranding:0:GameId"] = GameAId.ToString(),
                    ["Sodalis:Modules:Messaging:GameBranding:0:BrandName"] = GameABrand,
                    ["Sodalis:Modules:Messaging:GameBranding:0:PrimaryColor"] = "#ff0000",
                    ["Sodalis:Modules:Messaging:GameBranding:1:GameId"] = GameBId.ToString(),
                    ["Sodalis:Modules:Messaging:GameBranding:1:BrandName"] = GameBBrand,
                    ["Sodalis:Modules:Messaging:GameBranding:1:PrimaryColor"] = "#00ff00",
                    ["Sodalis:Modules:Messaging:LinkBaseUrls:Verification"] = VerificationLinkBase,
                    ["Sodalis:Modules:Messaging:LinkBaseUrls:PasswordReset"] = PasswordResetLinkBase,
                    ["Sodalis:Modules:Messaging:TokenLifetimes:VerificationHours"] = "24",
                    ["Sodalis:Modules:Messaging:TokenLifetimes:PasswordResetMinutes"] = "60"
                });
            });

            builder.ConfigureTestServices(services =>
            {
                // Replace real SMTP provider with a capture-only fake so tests
                // can inspect outgoing email without touching the network.
                services.RemoveAll<IEmailProvider>();
                services.AddSingleton<IEmailProvider>(Emails);
            });
        });

        // Trigger host build + migrations + seeding once before any test runs.
        using var primer = App.CreateClient();
    }

    public async Task DisposeAsync()
    {
        await App.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    /// <summary>
    /// Creates an HttpClient pre-configured with the API key for one of the two seeded test games.
    /// Pass null for GameA (the default); pass GameBId to target GameB.
    /// </summary>
    public HttpClient CreateClient(Guid? gameId = null)
    {
        var (_, key) = ResolveGame(gameId);
        var client = App.CreateClient();
        client.DefaultRequestHeaders.Add(ApiKeyMiddleware.HeaderName, key);
        return client;
    }

    /// <summary>
    /// HttpClient WITHOUT API key — for middleware bypass tests (health, openapi) or
    /// for testing 401 on missing key.
    /// </summary>
    public HttpClient CreateClientWithoutApiKey() => App.CreateClient();

    /// <summary>
    /// HttpClient with an explicit raw API key (for testing invalid / revoked keys).
    /// </summary>
    public HttpClient CreateClientWithRawKey(string rawApiKey)
    {
        var client = App.CreateClient();
        client.DefaultRequestHeaders.Add(ApiKeyMiddleware.HeaderName, rawApiKey);
        return client;
    }

    public IServiceScope CreateScope() => App.Services.CreateScope();

    private (Guid Id, string Key) ResolveGame(Guid? gameId)
    {
        if (gameId is null || gameId == GameAId)
            return (GameAId, GameAKey);
        if (gameId == GameBId)
            return (GameBId, GameBKey);

        throw new ArgumentException(
            $"Unknown gameId '{gameId}'. Only seeded games ({GameAId}, {GameBId}) are supported. " +
            "If your test needs a third game, seed it via SodalisFixture.",
            nameof(gameId));
    }
}

[CollectionDefinition(nameof(SodalisCollection))]
public sealed class SodalisCollection : ICollectionFixture<SodalisFixture>;
