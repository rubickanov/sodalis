using System.Net.Http.Json;
using Shouldly;
using Sodalis.IntegrationTests.Infrastructure;

namespace Sodalis.IntegrationTests.Identity;

[Collection(nameof(SodalisCollection))]
public class MultiTenancyTests(SodalisFixture fixture)
{
    [Fact]
    public async Task SameEmail_InDifferentGames_ProducesDifferentPlayers()
    {
        var email = $"u{Guid.NewGuid():N}@test.local";
        var password = "validpass123";

        var gameA = fixture.CreateClient(fixture.GameAId);
        var gameB = fixture.CreateClient(fixture.GameBId);

        var inA = (await (await gameA.PostAsJsonAsync("/api/v1/auth/register", new { email, password }))
            .Content.ReadFromJsonAsync<LoginLikeResponse>()).ShouldNotBeNull();

        var inB = (await (await gameB.PostAsJsonAsync("/api/v1/auth/register", new { email, password }))
            .Content.ReadFromJsonAsync<LoginLikeResponse>()).ShouldNotBeNull();

        inA.Player.PlayerId.ShouldNotBe(inB.Player.PlayerId,
            "the same email in two different games must resolve to two distinct players");
    }
}
