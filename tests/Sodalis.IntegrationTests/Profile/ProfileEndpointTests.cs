using System.Net;
using System.Net.Http.Json;
using Shouldly;
using Sodalis.IntegrationTests.Infrastructure;

namespace Sodalis.IntegrationTests.Profile;

[Collection(nameof(SodalisCollection))]
public class ProfileEndpointTests(SodalisFixture fixture)
{
    [Fact]
    public async Task GetMe_WithoutAuth_Returns401()
    {
        var client = fixture.CreateClient();

        var response = await client.GetAsync("/api/v1/profile/me");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMe_WithJwt_AutoCreatesProfile()
    {
        var (client, playerId) = await fixture.CreateAnonymousAsync();

        var response = await client.GetAsync("/api/v1/profile/me");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = (await response.Content.ReadFromJsonAsync<ProfileShape>()).ShouldNotBeNull();
        body.PlayerId.ShouldBe(playerId);
        body.DisplayName.ShouldStartWith("Player-");
        body.AvatarUrl.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetMe_Twice_ReturnsSameProfile()
    {
        var (client, _) = await fixture.CreateAnonymousAsync();

        var first = (await (await client.GetAsync("/api/v1/profile/me"))
            .Content.ReadFromJsonAsync<ProfileShape>()).ShouldNotBeNull();
        var second = (await (await client.GetAsync("/api/v1/profile/me"))
            .Content.ReadFromJsonAsync<ProfileShape>()).ShouldNotBeNull();

        second.PlayerId.ShouldBe(first.PlayerId);
        second.DisplayName.ShouldBe(first.DisplayName);
        second.CreatedAt.ShouldBe(first.CreatedAt);
    }

    [Fact]
    public async Task PatchMe_UpdatesFields_SubsequentGetReflectsChanges()
    {
        var (client, _) = await fixture.CreateAnonymousAsync();

        var patch = await client.PatchAsJsonAsync("/api/v1/profile/me", new {
            displayName = "Pingvin",
            avatarUrl = "https://i.imgur.com/abc.png"
        });
        patch.StatusCode.ShouldBe(HttpStatusCode.OK);

        var get = await client.GetAsync("/api/v1/profile/me");
        var body = (await get.Content.ReadFromJsonAsync<ProfileShape>()).ShouldNotBeNull();
        body.DisplayName.ShouldBe("Pingvin");
        body.AvatarUrl.ShouldBe("https://i.imgur.com/abc.png");
    }

    [Fact]
    public async Task PatchMe_WithInvalidAvatarUrl_Returns400()
    {
        var (client, _) = await fixture.CreateAnonymousAsync();

        var patch = await client.PatchAsJsonAsync("/api/v1/profile/me", new {
            avatarUrl = "not-a-real-url"
        });

        patch.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PatchMe_WithEmptyAvatarUrl_ClearsAvatar()
    {
        var (client, _) = await fixture.CreateAnonymousAsync();

        await client.PatchAsJsonAsync("/api/v1/profile/me", new {
            avatarUrl = "https://example.com/a.png"
        });
        await client.PatchAsJsonAsync("/api/v1/profile/me", new { avatarUrl = "" });

        var body = (await (await client.GetAsync("/api/v1/profile/me"))
            .Content.ReadFromJsonAsync<ProfileShape>()).ShouldNotBeNull();
        body.AvatarUrl.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetById_SameGame_ReturnsProfile()
    {
        var gameId = fixture.GameAId;
        var (clientA, playerIdA) = await fixture.CreateAnonymousAsync(gameId);
        await clientA.PatchAsJsonAsync("/api/v1/profile/me", new { displayName = "Alice" });

        var (clientB, _) = await fixture.CreateAnonymousAsync(gameId);
        var response = await clientB.GetAsync($"/api/v1/profile/{playerIdA}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = (await response.Content.ReadFromJsonAsync<ProfileShape>()).ShouldNotBeNull();
        body.PlayerId.ShouldBe(playerIdA);
        body.DisplayName.ShouldBe("Alice");
    }

    [Fact]
    public async Task GetById_DifferentGame_Returns404()
    {
        var (clientGameA, playerIdInGameA) = await fixture.CreateAnonymousAsync(fixture.GameAId);
        await clientGameA.GetAsync("/api/v1/profile/me");

        var (clientGameB, _) = await fixture.CreateAnonymousAsync(fixture.GameBId);
        var response = await clientGameB.GetAsync($"/api/v1/profile/{playerIdInGameA}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetById_NonExistent_Returns404()
    {
        var (client, _) = await fixture.CreateAnonymousAsync();

        var response = await client.GetAsync($"/api/v1/profile/{Guid.NewGuid()}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}

public sealed record ProfileShape(
    Guid PlayerId,
    Guid GameId,
    string DisplayName,
    string AvatarUrl,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
