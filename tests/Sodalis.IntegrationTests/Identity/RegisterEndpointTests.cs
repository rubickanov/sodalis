using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sodalis.IntegrationTests.Infrastructure;
using Sodalis.Modules.Identity.Persistence;

namespace Sodalis.IntegrationTests.Identity;

[Collection(nameof(SodalisCollection))]
public class RegisterEndpointTests(SodalisFixture fixture)
{
    [Fact]
    public async Task Register_WithValidInput_ReturnsTokensAndCreatesPlayer()
    {
        var gameId = Guid.NewGuid();
        var client = fixture.CreateClient(gameId);
        var email = $"u{Guid.NewGuid():N}@test.local";

        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            password = "supersecret123"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<LoginLikeResponse>();
        body.Should().NotBeNull();
        body!.AccessToken.Should().NotBeNullOrEmpty();
        body.RefreshToken.Should().NotBeNullOrEmpty();
        body.Player.IsNew.Should().BeTrue();
        body.Player.LinkedProviders.Should().Contain("email");

        using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var identity = await db.ExternalIdentities
            .FirstOrDefaultAsync(ei => ei.GameId == gameId && ei.ProviderId == "email" && ei.ExternalId == email);

        identity.Should().NotBeNull();
        identity!.Metadata.Should().NotBeNull();
        identity.Metadata.Should().Contain("$argon2id$", "password must be hashed, not stored as plaintext");
        identity.Metadata.Should().NotContain("supersecret123", "plaintext password must never appear in metadata");
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_Returns400()
    {
        var gameId = Guid.NewGuid();
        var client = fixture.CreateClient(gameId);
        var email = $"u{Guid.NewGuid():N}@test.local";

        var first = await client.PostAsJsonAsync("/api/v1/auth/register", new { email, password = "validpass1" });
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        var second = await client.PostAsJsonAsync("/api/v1/auth/register", new { email, password = "validpass1" });
        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_WithInvalidEmail_Returns400_WithValidationProblem()
    {
        var client = fixture.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email = "not-an-email",
            password = "validpass123"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblem>();
        problem.Should().NotBeNull();
        problem!.Errors.Should().ContainKey("Email");
    }
}
