using System.Net;
using System.Net.Http.Json;
using Shouldly;
using Sodalis.IntegrationTests.Infrastructure;

namespace Sodalis.IntegrationTests.Identity;

[Collection(nameof(SodalisCollection))]
public class ResetPasswordEndpointTests(SodalisFixture fixture)
{
    [Fact]
    public async Task ResetPassword_WithValidToken_ChangesPasswordAndRevokesSessions_AndNotifies()
    {
        fixture.Emails.Clear();
        var client = fixture.CreateClient();
        var email = $"u{Guid.NewGuid():N}@test.local";
        const string oldPassword = "oldpassword1";
        const string newPassword = "newpassword1";

        var register = await client.PostAsJsonAsync("/api/v1/auth/register",
            new { email, password = oldPassword });
        register.EnsureSuccessStatusCode();
        var registered = (await register.Content.ReadFromJsonAsync<LoginLikeResponse>()).ShouldNotBeNull();
        var originalRefresh = registered.RefreshToken;

        // forgot → captures reset email with token
        await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email });
        var resetEmail = (await fixture.Emails.WaitForAsync(m =>
            m.ToAddress == email && m.Subject.Contains("Reset"))).ShouldNotBeNull();
        var token = ExtractToken(resetEmail.HtmlBody, fixture.PasswordResetLinkBase);

        var resetResponse = await client.PostAsJsonAsync("/api/v1/auth/reset-password",
            new { token, newPassword });
        resetResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Old refresh token must be revoked.
        var oldRefreshTry = await client.PostAsJsonAsync("/api/v1/auth/refresh",
            new { refreshToken = originalRefresh });
        oldRefreshTry.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // Old password no longer works; new one does.
        var oldLogin = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            provider = "email",
            payload = new { email, password = oldPassword }
        });
        oldLogin.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var newLogin = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            provider = "email",
            payload = new { email, password = newPassword }
        });
        newLogin.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Password-changed notification was sent.
        var notification = await fixture.Emails.WaitForAsync(m =>
            m.ToAddress == email && m.Subject.Contains("password was changed"));
        notification.ShouldNotBeNull();
    }

    [Fact]
    public async Task ResetPassword_WithInvalidToken_Returns400()
    {
        var client = fixture.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/reset-password",
            new { token = "garbage-token", newPassword = "newpassword1" });
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ResetPassword_WithUsedToken_Returns400()
    {
        fixture.Emails.Clear();
        var client = fixture.CreateClient();
        var email = $"u{Guid.NewGuid():N}@test.local";

        await client.PostAsJsonAsync("/api/v1/auth/register",
            new { email, password = "oldpassword1" });
        await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email });

        var resetEmail = (await fixture.Emails.WaitForAsync(m =>
            m.ToAddress == email && m.Subject.Contains("Reset"))).ShouldNotBeNull();
        var token = ExtractToken(resetEmail.HtmlBody, fixture.PasswordResetLinkBase);

        var first = await client.PostAsJsonAsync("/api/v1/auth/reset-password",
            new { token, newPassword = "newpassword1" });
        first.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var second = await client.PostAsJsonAsync("/api/v1/auth/reset-password",
            new { token, newPassword = "anotherpassword1" });
        second.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ResetPassword_WithTooShortPassword_Returns400_WithValidationProblem()
    {
        var client = fixture.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/reset-password",
            new { token = "anything", newPassword = "short" });
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var problem = (await response.Content.ReadFromJsonAsync<ValidationProblem>()).ShouldNotBeNull();
        problem.Errors.ShouldContainKey("NewPassword");
    }

    private static string ExtractToken(string body, string baseUrl)
    {
        var idx = body.IndexOf(baseUrl, StringComparison.Ordinal);
        idx.ShouldBeGreaterThanOrEqualTo(0);
        var queryStart = body.IndexOf("token=", idx, StringComparison.Ordinal);
        queryStart.ShouldBeGreaterThanOrEqualTo(0);
        var tokenStart = queryStart + "token=".Length;
        var tokenEnd = tokenStart;
        while (tokenEnd < body.Length
               && body[tokenEnd] is not (' ' or '<' or '"' or '\r' or '\n' or '&'))
        {
            tokenEnd++;
        }
        return body[tokenStart..tokenEnd];
    }
}
