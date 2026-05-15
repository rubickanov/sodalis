using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Sodalis.IntegrationTests.Infrastructure;

namespace Sodalis.IntegrationTests.Identity;

[Collection(nameof(SodalisCollection))]
public class LoginEndpointTests(SodalisFixture fixture)
{
    [Fact]
    public async Task AnonymousLogin_CreatesNewPlayer_AndReturnsTokens()
    {
        var client = fixture.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            provider = "anonymous",
            payload = new { }
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<LoginLikeResponse>();
        body.Should().NotBeNull();
        body!.AccessToken.Should().NotBeNullOrEmpty();
        body.RefreshToken.Should().NotBeNullOrEmpty();
        body.Player.IsNew.Should().BeTrue();
        body.Player.LinkedProviders.Should().Contain("anonymous");
    }

    [Fact]
    public async Task EmailLogin_AfterRegister_ReturnsSamePlayer()
    {
        var client = fixture.CreateClient();
        var email = $"u{Guid.NewGuid():N}@test.local";
        var password = "verysecret123";

        var register = await client.PostAsJsonAsync("/api/v1/auth/register", new { email, password });
        register.EnsureSuccessStatusCode();
        var registered = (await register.Content.ReadFromJsonAsync<LoginLikeResponse>())!;

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            provider = "email",
            payload = new { email, password }
        });
        login.StatusCode.Should().Be(HttpStatusCode.OK);

        var loggedIn = (await login.Content.ReadFromJsonAsync<LoginLikeResponse>())!;
        loggedIn.Player.PlayerId.Should().Be(registered.Player.PlayerId, "login must resolve to the same player as register");
        loggedIn.Player.IsNew.Should().BeFalse();
    }

    [Fact]
    public async Task EmailLogin_WithWrongPassword_Returns401_WithGenericMessage()
    {
        var client = fixture.CreateClient();
        var email = $"u{Guid.NewGuid():N}@test.local";

        await client.PostAsJsonAsync("/api/v1/auth/register", new { email, password = "rightpassword" });

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            provider = "email",
            payload = new { email, password = "wrongpassword" }
        });

        login.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await login.Content.ReadAsStringAsync();
        body.Should().Contain("Invalid email or password");
    }

    [Fact]
    public async Task EmailLogin_WithNonExistentEmail_ReturnsSameMessageAsWrongPassword()
    {
        var client = fixture.CreateClient();

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            provider = "email",
            payload = new { email = "ghost@nowhere.local", password = "anything" }
        });

        login.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await login.Content.ReadAsStringAsync();
        body.Should().Contain("Invalid email or password",
            "anti-enumeration: must not differ from wrong-password message");
    }
}
