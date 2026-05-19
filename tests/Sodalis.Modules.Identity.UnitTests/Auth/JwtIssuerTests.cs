using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Shouldly;
using Sodalis.Modules.Identity.Auth;

namespace Sodalis.Modules.Identity.UnitTests.Auth;

public class JwtIssuerTests
{
    private const string ValidKey = "0123456789abcdef0123456789abcdef"; // exactly 32 ASCII bytes
    private const string Issuer = "sodalis-test";
    private const string Audience = "sodalis-test-aud";

    private static JwtIssuer Create(JwtSettings? settings = null) =>
        new(Options.Create(settings ?? new JwtSettings
        {
            Issuer = Issuer,
            Audience = Audience,
            SigningKey = ValidKey,
            AccessTokenLifetimeMinutes = 15
        }));

    [Fact]
    public void Issue_SetsClaims_FromArguments()
    {
        var sut = Create();
        var playerId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        var issued = sut.Issue(playerId, gameId, ["anonymous", "email"]);

        var handler = new JsonWebTokenHandler();
        var token = handler.ReadJsonWebToken(issued.Value);

        token.GetClaim(JwtRegisteredClaimNames.Sub).Value.ShouldBe(playerId.ToString());
        token.GetClaim("gid").Value.ShouldBe(gameId.ToString());
        token.GetClaim("auth").Value.ShouldBe("anonymous,email");
        token.Issuer.ShouldBe(Issuer);
        token.Audiences.ShouldContain(Audience);
    }

    [Fact]
    public void Issue_SetsExpiration_FromSettings()
    {
        var sut = Create(new JwtSettings
        {
            Issuer = Issuer,
            Audience = Audience,
            SigningKey = ValidKey,
            AccessTokenLifetimeMinutes = 30
        });

        var before = DateTimeOffset.UtcNow;
        var issued = sut.Issue(Guid.NewGuid(), Guid.NewGuid(), ["anonymous"]);
        var after = DateTimeOffset.UtcNow;

        issued.ExpiresAt.ShouldBeGreaterThanOrEqualTo(before.AddMinutes(30).AddSeconds(-2));
        issued.ExpiresAt.ShouldBeLessThanOrEqualTo(after.AddMinutes(30).AddSeconds(2));
    }

    [Fact]
    public async Task Issue_TokenValidates_WithSameKey()
    {
        var sut = Create();
        var issued = sut.Issue(Guid.NewGuid(), Guid.NewGuid(), ["anonymous"]);

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(issued.Value, new TokenValidationParameters
        {
            ValidIssuer = Issuer,
            ValidAudience = Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(ValidKey)),
            ValidateLifetime = false
        });

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task Issue_TokenDoesNotValidate_WithDifferentKey()
    {
        var sut = Create();
        var issued = sut.Issue(Guid.NewGuid(), Guid.NewGuid(), ["anonymous"]);

        var wrongKey = new string('z', 32);
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(issued.Value, new TokenValidationParameters
        {
            ValidIssuer = Issuer,
            ValidAudience = Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(wrongKey)),
            ValidateLifetime = false
        });

        result.IsValid.ShouldBeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Issue_Throws_WhenSigningKeyMissing(string key)
    {
        var sut = Create(new JwtSettings
        {
            Issuer = Issuer,
            Audience = Audience,
            SigningKey = key,
            AccessTokenLifetimeMinutes = 15
        });

        Should.Throw<InvalidOperationException>(
            () => sut.Issue(Guid.NewGuid(), Guid.NewGuid(), ["anonymous"]));
    }

    [Fact]
    public void Issue_Throws_WhenSigningKeyTooShort()
    {
        var sut = Create(new JwtSettings
        {
            Issuer = Issuer,
            Audience = Audience,
            SigningKey = new string('x', 31), // 31 bytes — one short of HS256 minimum
            AccessTokenLifetimeMinutes = 15
        });

        Should.Throw<InvalidOperationException>(
            () => sut.Issue(Guid.NewGuid(), Guid.NewGuid(), ["anonymous"]));
    }
}
