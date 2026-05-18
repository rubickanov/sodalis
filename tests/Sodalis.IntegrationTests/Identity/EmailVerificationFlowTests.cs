using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Sodalis.IntegrationTests.Infrastructure;
using Sodalis.Modules.Identity.AuthProviders;
using Sodalis.Modules.Identity.Persistence;

namespace Sodalis.IntegrationTests.Identity;

[Collection(nameof(SodalisCollection))]
public class EmailVerificationFlowTests(SodalisFixture fixture)
{
    [Fact]
    public async Task Register_SendsVerificationEmail()
    {
        fixture.Emails.Clear();
        var client = fixture.CreateClient();
        var email = $"u{Guid.NewGuid():N}@test.local";

        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            password = "validpass123"
        });
        response.EnsureSuccessStatusCode();

        var captured = await fixture.Emails.WaitForAsync(m => m.ToAddress == email);
        captured.ShouldNotBeNull();
        captured.Subject.ShouldContain("Verify");
        captured.HtmlBody.ShouldContain(fixture.VerificationLinkBase);
        captured.HtmlBody.ShouldContain("?token=");
        captured.TextBody.ShouldContain(fixture.VerificationLinkBase);
    }

    [Fact]
    public async Task VerifyEmail_WithValidToken_MarksEmailVerified()
    {
        fixture.Emails.Clear();
        var client = fixture.CreateClient();
        var email = $"u{Guid.NewGuid():N}@test.local";
        await client.PostAsJsonAsync("/api/v1/auth/register",
            new { email, password = "validpass123" });

        var captured = (await fixture.Emails.WaitForAsync(m => m.ToAddress == email)).ShouldNotBeNull();
        var token = ExtractToken(captured.HtmlBody, fixture.VerificationLinkBase);

        var verifyResponse = await client.PostAsJsonAsync("/api/v1/auth/verify-email", new { token });
        verifyResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var identity = (await db.ExternalIdentities
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(ei => ei.ExternalId == email && ei.ProviderId == EmailPasswordAuthProvider.Id))
            .ShouldNotBeNull();
        var meta = JsonSerializer.Deserialize<EmailMetadata>(identity.Metadata!).ShouldNotBeNull();
        meta.EmailVerified.ShouldBeTrue();
        meta.EmailVerifiedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task VerifyEmail_WithInvalidToken_Returns400()
    {
        var client = fixture.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/verify-email",
            new { token = "garbage-token" });
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task VerifyEmail_WithUsedToken_Returns400()
    {
        fixture.Emails.Clear();
        var client = fixture.CreateClient();
        var email = $"u{Guid.NewGuid():N}@test.local";
        await client.PostAsJsonAsync("/api/v1/auth/register",
            new { email, password = "validpass123" });

        var captured = (await fixture.Emails.WaitForAsync(m => m.ToAddress == email)).ShouldNotBeNull();
        var token = ExtractToken(captured.HtmlBody, fixture.VerificationLinkBase);

        var first = await client.PostAsJsonAsync("/api/v1/auth/verify-email", new { token });
        first.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var second = await client.PostAsJsonAsync("/api/v1/auth/verify-email", new { token });
        second.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
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
