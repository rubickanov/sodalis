using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Shouldly;
using Sodalis.IntegrationTests.Infrastructure;
using Sodalis.Modules.Tenancy.ApiKeys;
using Sodalis.Modules.Tenancy.Persistence;

namespace Sodalis.IntegrationTests.Tenancy;

[Collection(nameof(SodalisCollection))]
public class ApiKeyMiddlewareTests(SodalisFixture fixture)
{
    [Fact]
    public async Task RequestWithoutApiKey_To_ApiRoute_Returns401()
    {
        var client = fixture.CreateClientWithoutApiKey();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            provider = "anonymous",
            payload = new { }
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldContain(ApiKeyMiddleware.HeaderName);
    }

    [Fact]
    public async Task RequestWithInvalidApiKey_Returns401()
    {
        var client = fixture.CreateClientWithRawKey("sodalis_test_definitely_not_a_real_key_xxxxxxxxxxxxxxxxxxxx");

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            provider = "anonymous",
            payload = new { }
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RequestWithRevokedKey_Returns401()
    {
        // Provision a fresh key, revoke it, verify rejection. Uses a unique key so
        // we don't break the shared GameA seed.
        var rawKey = "sodalis_test_revoked_" + Guid.NewGuid().ToString("N");
        var hash = ApiKeyHasher.Hash(rawKey);

        using (var scope = fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TenancyDbContext>();
            db.GameApiKeys.Add(new Sodalis.Modules.Tenancy.Domain.GameApiKey
            {
                KeyHash = hash,
                GameId = fixture.GameAId,
                Prefix = ApiKeyHasher.Prefix(rawKey),
                Name = "revoked-for-test",
                CreatedAt = DateTimeOffset.UtcNow,
                RevokedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var client = fixture.CreateClientWithRawKey(rawKey);
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            provider = "anonymous",
            payload = new { }
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RequestWithValidKey_ResolvesGameContext()
    {
        var client = fixture.CreateClient(fixture.GameAId);

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            provider = "anonymous",
            payload = new { }
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = (await response.Content.ReadFromJsonAsync<LoginLikeResponse>()).ShouldNotBeNull();

        // JWT must carry the resolved gameId — decode the payload and check `gid`.
        var token = new JsonWebTokenHandler().ReadJsonWebToken(body.AccessToken);
        var gid = token.GetPayloadValue<string>("gid");
        Guid.Parse(gid).ShouldBe(fixture.GameAId);
    }

    [Fact]
    public async Task MismatchedJwtAndApiKey_Returns401()
    {
        // 1. Login as anonymous in GameA — get an access token whose `gid` claim is GameA.
        var gameAClient = fixture.CreateClient(fixture.GameAId);
        var login = await gameAClient.PostAsJsonAsync("/api/v1/auth/login", new
        {
            provider = "anonymous",
            payload = new { }
        });
        login.EnsureSuccessStatusCode();
        var body = (await login.Content.ReadFromJsonAsync<LoginLikeResponse>()).ShouldNotBeNull();

        // 2. Take that token but call /me with GameB's API key — must be rejected.
        var mismatched = fixture.CreateClient(fixture.GameBId);
        mismatched.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.AccessToken);

        var me = await mismatched.GetAsync("/api/v1/auth/me");
        me.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task HealthRoute_DoesNotRequireApiKey()
    {
        var client = fixture.CreateClientWithoutApiKey();

        var response = await client.GetAsync("/health/live");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task VersionRoute_DoesNotRequireApiKey()
    {
        var client = fixture.CreateClientWithoutApiKey();

        var response = await client.GetAsync("/version");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
