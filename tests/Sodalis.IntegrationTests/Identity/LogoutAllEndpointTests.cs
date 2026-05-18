using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Shouldly;
using Sodalis.IntegrationTests.Infrastructure;

namespace Sodalis.IntegrationTests.Identity;

[Collection(nameof(SodalisCollection))]
public class LogoutAllEndpointTests(SodalisFixture fixture)
{
    [Fact]
    public async Task WithoutAuth_Returns401()
    {
        var client = fixture.CreateClient();

        var response = await client.PostAsync("/api/v1/auth/logout-all", content: null);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RevokesAllPlayerRefreshTokens()
    {
        var gameId = fixture.GameAId;
        var email = $"u{Guid.NewGuid():N}@test.local";
        const string password = "validpass123";

        // First login — register also returns a refresh token.
        var first = await Register(fixture.CreateClient(gameId), email, password);

        // Second login as same player — gets another refresh token for the same PlayerId.
        var second = await EmailLogin(fixture.CreateClient(gameId), email, password);
        second.Player.PlayerId.ShouldBe(first.Player.PlayerId);

        // Authorize as the player and hit /logout-all.
        var authedClient = fixture.CreateClient(gameId);
        Authorize(authedClient, second.AccessToken);
        var logoutAll = await authedClient.PostAsync("/api/v1/auth/logout-all", content: null);
        logoutAll.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Both refresh tokens must now be invalid.
        var firstRefresh = await fixture.CreateClient(gameId).PostAsJsonAsync(
            "/api/v1/auth/refresh", new { refreshToken = first.RefreshToken });
        firstRefresh.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var secondRefresh = await fixture.CreateClient(gameId).PostAsJsonAsync(
            "/api/v1/auth/refresh", new { refreshToken = second.RefreshToken });
        secondRefresh.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DoesNotAffectOtherPlayer()
    {
        var gameId = fixture.GameAId;
        var alice = await Register(fixture.CreateClient(gameId),
            $"alice{Guid.NewGuid():N}@test.local", "validpass123");
        var bob = await Register(fixture.CreateClient(gameId),
            $"bob{Guid.NewGuid():N}@test.local", "validpass123");

        var aliceClient = fixture.CreateClient(gameId);
        Authorize(aliceClient, alice.AccessToken);
        var logoutAll = await aliceClient.PostAsync("/api/v1/auth/logout-all", content: null);
        logoutAll.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Bob's refresh token must still be valid.
        var bobRefresh = await fixture.CreateClient(gameId).PostAsJsonAsync(
            "/api/v1/auth/refresh", new { refreshToken = bob.RefreshToken });
        bobRefresh.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static async Task<LoginLikeResponse> Register(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new { email, password });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LoginLikeResponse>()).ShouldNotBeNull();
    }

    private static async Task<LoginLikeResponse> EmailLogin(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            provider = "email",
            payload = new { email, password }
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LoginLikeResponse>()).ShouldNotBeNull();
    }

    private static void Authorize(HttpClient client, string accessToken) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
}
