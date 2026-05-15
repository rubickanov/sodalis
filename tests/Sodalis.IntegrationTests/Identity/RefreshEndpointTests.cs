using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Sodalis.IntegrationTests.Infrastructure;

namespace Sodalis.IntegrationTests.Identity;

[Collection(nameof(SodalisCollection))]
public class RefreshEndpointTests(SodalisFixture fixture)
{
    [Fact]
    public async Task Refresh_ValidToken_RotatesAndReturnsNewPair()
    {
        var client = fixture.CreateClient();
        var login = await AnonymousLogin(client);

        var response = await client.PostAsJsonAsync("/api/v1/auth/refresh", new
        {
            refreshToken = login.RefreshToken
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = (await response.Content.ReadFromJsonAsync<LoginLikeResponse>())!;
        body.AccessToken.Should().NotBeNullOrEmpty();
        body.RefreshToken.Should().NotBe(login.RefreshToken, "refresh must rotate");
        body.Player.PlayerId.Should().Be(login.Player.PlayerId);
    }

    [Fact]
    public async Task Refresh_ReusingOldToken_AfterRotation_ReturnsSessionCompromised()
    {
        var client = fixture.CreateClient();
        var login = await AnonymousLogin(client);

        // First rotation succeeds.
        var rotated = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = login.RefreshToken });
        rotated.EnsureSuccessStatusCode();
        var newPair = (await rotated.Content.ReadFromJsonAsync<LoginLikeResponse>())!;

        // Reusing the OLD token must be rejected and entire chain killed.
        var reuse = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = login.RefreshToken });
        reuse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await reuse.Content.ReadAsStringAsync();
        body.Should().Contain("session_compromised");

        // The previously-rotated NEW token should also be invalid now (chain revoked).
        var afterReuse = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = newPair.RefreshToken });
        afterReuse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static async Task<LoginLikeResponse> AnonymousLogin(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            provider = "anonymous",
            payload = new { }
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LoginLikeResponse>())!;
    }
}
