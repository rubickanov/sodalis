using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Sodalis.IntegrationTests.Infrastructure;
using Sodalis.Modules.Profile.Persistence;

namespace Sodalis.IntegrationTests.Profile;

[Collection(nameof(SodalisCollection))]
public class ProfileRaceTests(SodalisFixture fixture)
{
    [Fact]
    public async Task ConcurrentGetMe_CreatesExactlyOneProfile()
    {
        const int parallelism = 20;
        var (client, playerId) = await fixture.CreateAnonymousAsync();

        var tasks = Enumerable.Range(0, parallelism)
            .Select(_ => client.GetAsync("/api/v1/profile/me"))
            .ToArray();

        var responses = await Task.WhenAll(tasks);

        foreach (var r in responses)
            r.StatusCode.ShouldBe(HttpStatusCode.OK);

        var bodies = await Task.WhenAll(
            responses.Select(r => r.Content.ReadFromJsonAsync<ProfileShape>()));

        bodies.ShouldAllBe(b => b != null);
        var first = bodies[0]!;
        foreach (var b in bodies)
        {
            b!.PlayerId.ShouldBe(first.PlayerId);
            // Same CreatedAt across all responses proves exactly one profile
            // was created and concurrent readers observed the same row.
            b.CreatedAt.ShouldBe(first.CreatedAt);
        }

        using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProfileDbContext>();
        var rowCount = await db.Profiles
            .IgnoreQueryFilters()
            .CountAsync(p => p.PlayerId == playerId);
        rowCount.ShouldBe(1);
    }
}
