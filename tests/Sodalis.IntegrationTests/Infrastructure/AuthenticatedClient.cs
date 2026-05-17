using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Sodalis.IntegrationTests.Infrastructure;

public static class AuthenticatedClient
{
    public static async Task<(HttpClient Client, Guid PlayerId)> CreateAnonymousAsync(
        this SodalisFixture fixture,
        Guid? gameId = null)
    {
        var client = fixture.CreateClient(gameId);

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new {
            provider = "anonymous",
            payload = new { }
        });
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<LoginLikeResponse>()
            ?? throw new InvalidOperationException("Failed to deserialize login response.");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.AccessToken);

        return (client, body.Player.PlayerId);
    }
}
