using System.Text.Json;
using Shouldly;
using Sodalis.Modules.Identity.AuthProviders;

namespace Sodalis.Modules.Identity.UnitTests.AuthProviders;

public class AnonymousAuthProviderTests
{
    private readonly AnonymousAuthProvider _sut = new();

    [Fact]
    public void ProviderId_IsAnonymous()
    {
        _sut.ProviderId.ShouldBe("anonymous");
    }

    [Fact]
    public async Task Authenticate_ReturnsOk_WithGuidExternalId()
    {
        var result = await _sut.AuthenticateAsync(
            new AuthRequest("anonymous", JsonDocument.Parse("{}").RootElement, Guid.NewGuid()),
            CancellationToken.None);

        result.Success.ShouldBeTrue();
        result.ExternalId.ShouldNotBeNull();
        Guid.TryParseExact(result.ExternalId, "N", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Authenticate_GeneratesUniqueExternalId_EachCall()
    {
        const int n = 100;
        var ids = new HashSet<string>();
        for (var i = 0; i < n; i++)
        {
            var result = await _sut.AuthenticateAsync(
                new AuthRequest("anonymous", JsonDocument.Parse("{}").RootElement, Guid.NewGuid()),
                CancellationToken.None);
            ids.Add(result.ExternalId!);
        }
        ids.Count.ShouldBe(n);
    }

    [Fact]
    public async Task Authenticate_IgnoresPayloadAndGameId()
    {
        // Anonymous provider has no game-scoped or payload-derived behaviour.
        // Two different inputs still both succeed with fresh ids.
        var first = await _sut.AuthenticateAsync(
            new AuthRequest("anonymous", JsonDocument.Parse("{\"junk\":1}").RootElement, Guid.NewGuid()),
            CancellationToken.None);
        var second = await _sut.AuthenticateAsync(
            new AuthRequest("anonymous", JsonDocument.Parse("{}").RootElement, Guid.NewGuid()),
            CancellationToken.None);

        first.Success.ShouldBeTrue();
        second.Success.ShouldBeTrue();
        first.ExternalId.ShouldNotBe(second.ExternalId);
    }
}
