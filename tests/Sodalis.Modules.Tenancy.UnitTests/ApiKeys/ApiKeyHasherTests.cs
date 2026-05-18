using Shouldly;
using Sodalis.Modules.Tenancy.ApiKeys;

namespace Sodalis.Modules.Tenancy.UnitTests.ApiKeys;

public class ApiKeyHasherTests
{
    [Fact]
    public void Hash_IsDeterministic()
    {
        var a = ApiKeyHasher.Hash("sodalis_dev_abc");
        var b = ApiKeyHasher.Hash("sodalis_dev_abc");

        a.ShouldBe(b);
    }

    [Fact]
    public void Hash_DiffersForDifferentInputs()
    {
        var a = ApiKeyHasher.Hash("sodalis_dev_abc");
        var b = ApiKeyHasher.Hash("sodalis_dev_xyz");

        a.ShouldNotBe(b);
    }

    [Fact]
    public void Hash_Returns64HexChars()
    {
        var hash = ApiKeyHasher.Hash("anything");

        hash.Length.ShouldBe(64);
        hash.ShouldMatch("^[0-9A-F]{64}$");
    }

    [Fact]
    public void Prefix_ReturnsFirst16Chars()
    {
        var prefix = ApiKeyHasher.Prefix("sodalis_dev_abcdefghijklmnopqrstuvwxyz");

        prefix.ShouldBe("sodalis_dev_abcd");
        prefix.Length.ShouldBe(16);
    }

    [Fact]
    public void Prefix_ReturnsFullString_WhenShorterThan16()
    {
        var prefix = ApiKeyHasher.Prefix("short");

        prefix.ShouldBe("short");
    }

    [Fact]
    public void Prefix_ReturnsFullString_WhenExactly16()
    {
        var prefix = ApiKeyHasher.Prefix("1234567890123456");

        prefix.ShouldBe("1234567890123456");
    }

    [Fact]
    public void GenerateRaw_HasExpectedFormat()
    {
        var raw = ApiKeyHasher.GenerateRaw("dev");

        raw.ShouldStartWith("sodalis_dev_");
        raw.Length.ShouldBeGreaterThan("sodalis_dev_".Length + 32);
    }

    [Fact]
    public void GenerateRaw_ProducesDifferentValues()
    {
        var a = ApiKeyHasher.GenerateRaw("dev");
        var b = ApiKeyHasher.GenerateRaw("dev");

        a.ShouldNotBe(b);
    }
}
