using System.Net;
using System.Net.Http.Json;
using Shouldly;
using Sodalis.IntegrationTests.Infrastructure;

namespace Sodalis.IntegrationTests.Identity;

[Collection(nameof(SodalisCollection))]
public class LogoutEndpointTests(SodalisFixture fixture)
{
    [Fact]
    public async Task WithoutApiKey_Returns401()
    {
        var client = fixture.CreateClientWithoutApiKey();

        var response = await client.PostAsJsonAsync("/api/v1/auth/logout", new { refreshToken = "anything" });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ValidToken_RevokesAndReturns204()
    {
        var gameId = fixture.GameAId;
        var registered = await Register(fixture.CreateClient(gameId),
            $"u{Guid.NewGuid():N}@test.local", "validpass123");

        var logout = await fixture.CreateClient(gameId)
            .PostAsJsonAsync("/api/v1/auth/logout", new { refreshToken = registered.RefreshToken });
        logout.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // The same refresh token must no longer be valid.
        var refresh = await fixture.CreateClient(gameId)
            .PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = registered.RefreshToken });
        refresh.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UnknownToken_Returns204_AntiEnumeration()
    {
        var response = await fixture.CreateClient()
            .PostAsJsonAsync("/api/v1/auth/logout", new { refreshToken = "definitely-not-a-real-token" });

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task AlreadyRevoked_Returns204()
    {
        var gameId = fixture.GameAId;
        var registered = await Register(fixture.CreateClient(gameId),
            $"u{Guid.NewGuid():N}@test.local", "validpass123");

        var first = await fixture.CreateClient(gameId)
            .PostAsJsonAsync("/api/v1/auth/logout", new { refreshToken = registered.RefreshToken });
        first.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var second = await fixture.CreateClient(gameId)
            .PostAsJsonAsync("/api/v1/auth/logout", new { refreshToken = registered.RefreshToken });
        second.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task WrongGame_DoesNotRevoke()
    {
        // Issuing game and logout game differ — logout must not touch the token,
        // and the original game can still refresh successfully.
        var registered = await Register(fixture.CreateClient(fixture.GameAId),
            $"u{Guid.NewGuid():N}@test.local", "validpass123");

        var logoutFromB = await fixture.CreateClient(fixture.GameBId)
            .PostAsJsonAsync("/api/v1/auth/logout", new { refreshToken = registered.RefreshToken });
        logoutFromB.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var refreshFromA = await fixture.CreateClient(fixture.GameAId)
            .PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = registered.RefreshToken });
        refreshFromA.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static async Task<LoginLikeResponse> Register(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new { email, password });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LoginLikeResponse>()).ShouldNotBeNull();
    }
}
