using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Sodalis.Modules.Tenancy.ApiKeys;

public static class ApiKeyHasher
{
    public const int PrefixLength = 16;

    public static string Hash(string rawKey)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(rawKey), hash);
        return Convert.ToHexString(hash);
    }

    public static string Prefix(string rawKey) =>
        rawKey.Length <= PrefixLength ? rawKey : rawKey[..PrefixLength];

    public static string GenerateRaw(string environmentLabel)
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return $"sodalis_{environmentLabel}_{Base64Url.EncodeToString(bytes)}";
    }
}
