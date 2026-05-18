using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Shouldly;
using Sodalis.IntegrationTests.Infrastructure;

namespace Sodalis.IntegrationTests.Identity;

[Collection(nameof(SodalisCollection))]
public class ChangePasswordEndpointTests(SodalisFixture fixture)
{
    [Fact]
    public async Task WithoutAuth_Returns401()
    {
        var client = fixture.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/change-password", new
        {
            currentPassword = "oldpassword1",
            newPassword = "newpassword1"
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task WithCorrectCurrent_ReturnsNewTokenPair_AndRevokesOldRefreshTokens()
    {
        var client = fixture.CreateClient();
        var email = $"u{Guid.NewGuid():N}@test.local";
        var oldPassword = "oldpassword1";
        var newPassword = "newpassword1";

        var registered = await Register(client, email, oldPassword);
        Authorize(client, registered.AccessToken);

        var response = await client.PostAsJsonAsync("/api/v1/auth/change-password", new
        {
            currentPassword = oldPassword,
            newPassword
        });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = (await response.Content.ReadFromJsonAsync<LoginLikeResponse>()).ShouldNotBeNull();
        body.AccessToken.ShouldNotBeNullOrEmpty();
        body.RefreshToken.ShouldNotBeNullOrEmpty();
        body.RefreshToken.ShouldNotBe(registered.RefreshToken, "change-password must rotate refresh token");
        body.Player.PlayerId.ShouldBe(registered.Player.PlayerId);
        body.Player.IsNew.ShouldBeFalse();

        // Old refresh token must be revoked.
        var oldRefresh = await client.PostAsJsonAsync("/api/v1/auth/refresh", new
        {
            refreshToken = registered.RefreshToken
        });
        oldRefresh.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // New refresh token must work.
        var newRefresh = await client.PostAsJsonAsync("/api/v1/auth/refresh", new
        {
            refreshToken = body.RefreshToken
        });
        newRefresh.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task WithWrongCurrent_Returns400()
    {
        var client = fixture.CreateClient();
        var email = $"u{Guid.NewGuid():N}@test.local";

        var registered = await Register(client, email, "rightpassword");
        Authorize(client, registered.AccessToken);

        var response = await client.PostAsJsonAsync("/api/v1/auth/change-password", new
        {
            currentPassword = "wrongpassword",
            newPassword = "newpassword1"
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadAsStringAsync();
        problem.ShouldContain("Current password is incorrect");
    }

    [Fact]
    public async Task AnonymousUser_Returns400_NoPasswordSet()
    {
        var (client, _) = await fixture.CreateAnonymousAsync();

        var response = await client.PostAsJsonAsync("/api/v1/auth/change-password", new
        {
            currentPassword = "anything",
            newPassword = "newpassword1"
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadAsStringAsync();
        problem.ShouldContain("No password set");
    }

    [Fact]
    public async Task NewPasswordEqualsCurrent_Returns400()
    {
        var client = fixture.CreateClient();
        var email = $"u{Guid.NewGuid():N}@test.local";
        const string password = "samepassword1";

        var registered = await Register(client, email, password);
        Authorize(client, registered.AccessToken);

        var response = await client.PostAsJsonAsync("/api/v1/auth/change-password", new
        {
            currentPassword = password,
            newPassword = password
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadAsStringAsync();
        problem.ShouldContain("must differ from current");
    }

    [Fact]
    public async Task NewPasswordTooShort_Returns400_WithValidationProblem()
    {
        var client = fixture.CreateClient();
        var email = $"u{Guid.NewGuid():N}@test.local";

        var registered = await Register(client, email, "oldpassword1");
        Authorize(client, registered.AccessToken);

        var response = await client.PostAsJsonAsync("/api/v1/auth/change-password", new
        {
            currentPassword = "oldpassword1",
            newPassword = "short"
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = (await response.Content.ReadFromJsonAsync<ValidationProblem>()).ShouldNotBeNull();
        problem.Errors.ShouldContainKey("NewPassword");
    }

    [Fact]
    public async Task AfterChange_OldPasswordFails_NewPasswordSucceeds()
    {
        var gameId = fixture.GameAId;
        var client = fixture.CreateClient(gameId);
        var email = $"u{Guid.NewGuid():N}@test.local";
        const string oldPassword = "oldpassword1";
        const string newPassword = "newpassword1";

        var registered = await Register(client, email, oldPassword);
        Authorize(client, registered.AccessToken);

        await client.PostAsJsonAsync("/api/v1/auth/change-password", new
        {
            currentPassword = oldPassword,
            newPassword
        });

        // Fresh client (no Authorization header) — login from scratch.
        var freshClient = fixture.CreateClient(gameId);

        var oldLogin = await freshClient.PostAsJsonAsync("/api/v1/auth/login", new
        {
            provider = "email",
            payload = new { email, password = oldPassword }
        });
        oldLogin.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var newLogin = await freshClient.PostAsJsonAsync("/api/v1/auth/login", new
        {
            provider = "email",
            payload = new { email, password = newPassword }
        });
        newLogin.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = (await newLogin.Content.ReadFromJsonAsync<LoginLikeResponse>()).ShouldNotBeNull();
        body.Player.PlayerId.ShouldBe(registered.Player.PlayerId);
    }

    private static async Task<LoginLikeResponse> Register(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new { email, password });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LoginLikeResponse>()).ShouldNotBeNull();
    }

    private static void Authorize(HttpClient client, string accessToken) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
}
