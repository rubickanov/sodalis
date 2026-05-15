using System.Net;
using System.Net.Http.Json;
using Shouldly;
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

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = (await response.Content.ReadFromJsonAsync<LoginLikeResponse>()).ShouldNotBeNull();
        body.AccessToken.ShouldNotBeNullOrEmpty();
        body.RefreshToken.ShouldNotBe(login.RefreshToken, "refresh must rotate");
        body.Player.PlayerId.ShouldBe(login.Player.PlayerId);
    }

    [Fact]
    public async Task Refresh_ReusingOldToken_AfterRotation_ReturnsSessionCompromised()
    {
        var client = fixture.CreateClient();
        var login = await AnonymousLogin(client);

        // First rotation succeeds.
        var rotated = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = login.RefreshToken });
        rotated.EnsureSuccessStatusCode();
        var newPair = (await rotated.Content.ReadFromJsonAsync<LoginLikeResponse>()).ShouldNotBeNull();

        // Reusing the OLD token must be rejected and entire chain killed.
        var reuse = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = login.RefreshToken });
        reuse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var body = await reuse.Content.ReadAsStringAsync();
        body.ShouldContain("session_compromised");

        // The previously-rotated NEW token should also be invalid now (chain revoked).
        var afterReuse = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = newPair.RefreshToken });
        afterReuse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_ConcurrentRotationOfSameToken_ExactlyOneSucceeds()
    {
        // Regression test for the rotation race: without an atomic UPDATE in
        // ValidateAndRotateAsync, two concurrent refresh requests with the
        // same token could both succeed and bypass reuse detection.
        var client = fixture.CreateClient();
        var login = await AnonymousLogin(client);

        const int parallelism = 8;
        var responses = await Task.WhenAll(Enumerable.Range(0, parallelism).Select(_ =>
            client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = login.RefreshToken })));

        var ok = responses.Count(r => r.IsSuccessStatusCode);
        var unauth = responses.Count(r => r.StatusCode == HttpStatusCode.Unauthorized);

        ok.ShouldBe(1, "exactly one rotation must win the race");
        unauth.ShouldBe(parallelism - 1, "all losing rotations must be rejected");
    }

    private static async Task<LoginLikeResponse> AnonymousLogin(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            provider = "anonymous",
            payload = new { }
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LoginLikeResponse>()).ShouldNotBeNull();
    }
}
