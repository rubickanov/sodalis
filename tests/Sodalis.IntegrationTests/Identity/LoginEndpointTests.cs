using System.Net;
using System.Net.Http.Json;
using Shouldly;
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

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = (await response.Content.ReadFromJsonAsync<LoginLikeResponse>()).ShouldNotBeNull();
        body.AccessToken.ShouldNotBeNullOrEmpty();
        body.RefreshToken.ShouldNotBeNullOrEmpty();
        body.Player.IsNew.ShouldBeTrue();
        body.Player.LinkedProviders.ShouldContain("anonymous");
    }

    [Fact]
    public async Task EmailLogin_AfterRegister_ReturnsSamePlayer()
    {
        var client = fixture.CreateClient();
        var email = $"u{Guid.NewGuid():N}@test.local";
        var password = "verysecret123";

        var register = await client.PostAsJsonAsync("/api/v1/auth/register", new { email, password });
        register.EnsureSuccessStatusCode();
        var registered = (await register.Content.ReadFromJsonAsync<LoginLikeResponse>()).ShouldNotBeNull();

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            provider = "email",
            payload = new { email, password }
        });
        login.StatusCode.ShouldBe(HttpStatusCode.OK);

        var loggedIn = (await login.Content.ReadFromJsonAsync<LoginLikeResponse>()).ShouldNotBeNull();
        loggedIn.Player.PlayerId.ShouldBe(registered.Player.PlayerId, "login must resolve to the same player as register");
        loggedIn.Player.IsNew.ShouldBeFalse();
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

        login.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var body = await login.Content.ReadAsStringAsync();
        body.ShouldContain("Invalid email or password");
    }

    [Fact]
    public async Task EmailLogin_WithExcessivelyLongPassword_RejectedQuickly_WithoutHashing()
    {
        // Regression test for the Argon2 DoS vector: without an early length check,
        // the server would attempt to hash a multi-MB password and exhaust CPU/memory.
        // The provider must reject before calling PasswordHasher.Verify.
        var client = fixture.CreateClient();
        var hugePassword = new string('a', 100_000);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            provider = "email",
            payload = new { email = "anyone@test.local", password = hugePassword }
        });
        sw.Stop();

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        sw.ElapsedMilliseconds.ShouldBeLessThan(500,
            "must reject before running Argon2id (which would take seconds on huge input)");
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

        login.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var body = await login.Content.ReadAsStringAsync();
        body.ShouldContain("Invalid email or password",
            customMessage: "anti-enumeration: must not differ from wrong-password message");
    }
}
