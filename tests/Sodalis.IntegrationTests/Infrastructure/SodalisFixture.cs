using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Sodalis.IntegrationTests.Infrastructure;

public sealed class SodalisFixture : IAsyncLifetime
{
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
                    ["ConnectionStrings:Sodalis"] = _postgres.GetConnectionString()
                });
            });
        });

        // Trigger host build + migrations once before any test runs.
        using var primer = App.CreateClient();
    }

    public async Task DisposeAsync()
    {
        await App.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    public HttpClient CreateClient(Guid? gameId = null)
    {
        var client = App.CreateClient();
        client.DefaultRequestHeaders.Add("X-Game-Id", (gameId ?? Guid.NewGuid()).ToString());
        return client;
    }

    public IServiceScope CreateScope() => App.Services.CreateScope();
}

[CollectionDefinition(nameof(SodalisCollection))]
public sealed class SodalisCollection : ICollectionFixture<SodalisFixture>;
