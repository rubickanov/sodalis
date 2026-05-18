using System.Net;
using System.Net.Http.Json;
using Shouldly;
using Sodalis.IntegrationTests.Infrastructure;

namespace Sodalis.IntegrationTests.Identity;

[Collection(nameof(SodalisCollection))]
public class ForgotPasswordEndpointTests(SodalisFixture fixture)
{
    [Fact]
    public async Task ForgotPassword_WithExistingEmail_SendsResetEmail()
    {
        fixture.Emails.Clear();
        var client = fixture.CreateClient();
        var email = $"u{Guid.NewGuid():N}@test.local";
        await client.PostAsJsonAsync("/api/v1/auth/register",
            new { email, password = "validpass123" });

        // Skip the verification email captured during register.
        var response = await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email });
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var resetEmail = await fixture.Emails.WaitForAsync(m =>
            m.ToAddress == email && m.Subject.Contains("Reset"));
        resetEmail.ShouldNotBeNull();
        resetEmail.HtmlBody.ShouldContain(fixture.PasswordResetLinkBase);
        resetEmail.HtmlBody.ShouldContain("?token=");
    }

    [Fact]
    public async Task ForgotPassword_WithUnknownEmail_Returns204_AndDoesNotSendEmail()
    {
        fixture.Emails.Clear();
        var client = fixture.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/forgot-password",
            new { email = $"ghost{Guid.NewGuid():N}@nowhere.local" });

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // No email must have been queued. Give the fire-and-forget background task
        // a moment in case it would have happened.
        await Task.Delay(200);
        fixture.Emails.Messages.Any(m => m.Subject.Contains("Reset")).ShouldBeFalse(
            "anti-enumeration: must not send a reset email for unknown addresses");
    }

    [Fact]
    public async Task ForgotPassword_InvalidEmailFormat_Returns400()
    {
        var client = fixture.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/forgot-password",
            new { email = "not-an-email" });
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
