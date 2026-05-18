using System.Net.Http.Json;
using Shouldly;
using Sodalis.IntegrationTests.Infrastructure;

namespace Sodalis.IntegrationTests.Messaging;

[Collection(nameof(SodalisCollection))]
public class PerGameBrandingTests(SodalisFixture fixture)
{
    [Fact]
    public async Task EmailToGameA_UsesGameABranding_AndDistinctFromGameB()
    {
        fixture.Emails.Clear();

        var clientA = fixture.CreateClient(fixture.GameAId);
        var clientB = fixture.CreateClient(fixture.GameBId);

        var emailA = $"a{Guid.NewGuid():N}@test.local";
        var emailB = $"b{Guid.NewGuid():N}@test.local";

        await clientA.PostAsJsonAsync("/api/v1/auth/register",
            new { email = emailA, password = "validpass123" });
        await clientB.PostAsJsonAsync("/api/v1/auth/register",
            new { email = emailB, password = "validpass123" });

        var capturedA = (await fixture.Emails.WaitForAsync(m => m.ToAddress == emailA)).ShouldNotBeNull();
        var capturedB = (await fixture.Emails.WaitForAsync(m => m.ToAddress == emailB)).ShouldNotBeNull();

        capturedA.Subject.ShouldContain(fixture.GameABrand);
        capturedA.HtmlBody.ShouldContain(fixture.GameABrand);
        capturedA.HtmlBody.ShouldNotContain(fixture.GameBBrand);

        capturedB.Subject.ShouldContain(fixture.GameBBrand);
        capturedB.HtmlBody.ShouldContain(fixture.GameBBrand);
        capturedB.HtmlBody.ShouldNotContain(fixture.GameABrand);
    }
}
