using Shouldly;
using Sodalis.Modules.Identity.Auth;

namespace Sodalis.Modules.Identity.UnitTests.Auth;

public class PasswordHasherTests
{
    private readonly PasswordHasher _hasher = new();

    [Fact]
    public void Hash_ReturnsPhcFormattedString()
    {
        var hash = _hasher.Hash("hunter2hunter2");

        hash.ShouldStartWith("$argon2id$v=19$m=19456,t=2,p=1$");
        hash.Split('$').Length.ShouldBe(6);
    }

    [Fact]
    public void Verify_ReturnsTrue_ForCorrectPassword()
    {
        var password = "correct horse battery staple";
        var hash = _hasher.Hash(password);

        _hasher.Verify(password, hash).ShouldBeTrue();
    }

    [Fact]
    public void Verify_ReturnsFalse_ForWrongPassword()
    {
        var hash = _hasher.Hash("correct password");

        _hasher.Verify("wrong password", hash).ShouldBeFalse();
    }

    [Fact]
    public void Verify_ReturnsFalse_ForMalformedHash()
    {
        _hasher.Verify("anything", "not-a-real-hash").ShouldBeFalse();
        _hasher.Verify("anything", "$argon2id$broken").ShouldBeFalse();
        _hasher.Verify("anything", "").ShouldBeFalse();
    }

    [Fact]
    public void Hash_ProducesDifferentOutput_ForSamePassword()
    {
        var password = "samepassword";

        var first = _hasher.Hash(password);
        var second = _hasher.Hash(password);

        first.ShouldNotBe(second, "salt is randomized per hash");
    }
}
