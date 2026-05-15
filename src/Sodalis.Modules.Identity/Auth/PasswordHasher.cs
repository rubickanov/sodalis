using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace Sodalis.Modules.Identity.Auth;

public sealed class PasswordHasher
{
    // OWASP-recommended Argon2id parameters (2024).
    // m=19 MiB, t=2 iterations, p=1 thread.
    private const int MemoryKiB = 19456;
    private const int Iterations = 2;
    private const int Parallelism = 1;
    private const int SaltBytes = 16;
    private const int HashBytes = 32;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = ComputeHash(password, salt, MemoryKiB, Iterations, Parallelism);

        return $"$argon2id$v=19$m={MemoryKiB},t={Iterations},p={Parallelism}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public bool Verify(string password, string phcHash)
    {
        if (!TryParsePhc(phcHash, out var parsed))
        {
            return false;
        }

        var computed = ComputeHash(password, parsed.Salt, parsed.Memory, parsed.Iterations, parsed.Parallelism);
        return CryptographicOperations.FixedTimeEquals(computed, parsed.Hash);
    }

    private static byte[] ComputeHash(string password, byte[] salt, int memoryKiB, int iterations, int parallelism)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            MemorySize = memoryKiB,
            Iterations = iterations,
            DegreeOfParallelism = parallelism
        };
        return argon2.GetBytes(HashBytes);
    }

    private static bool TryParsePhc(string phc, out PhcParts parts)
    {
        parts = default;

        // Expected: $argon2id$v=19$m=<m>,t=<t>,p=<p>$<base64 salt>$<base64 hash>
        var segments = phc.Split('$', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 5 || segments[0] != "argon2id")
        {
            return false;
        }

        if (segments[1] != "v=19")
        {
            return false;
        }

        var paramPairs = segments[2].Split(',');
        int? memory = null;
        int? iterations = null;
        int? parallelism = null;
        foreach (var pair in paramPairs)
        {
            var kv = pair.Split('=');
            if (kv.Length != 2)
            {
                return false;
            }

            if (!int.TryParse(kv[1], out var value))
            {
                return false;
            }

            switch (kv[0])
            {
                case "m": memory = value; break;
                case "t": iterations = value; break;
                case "p": parallelism = value; break;
            }
        }

        if (memory is null || iterations is null || parallelism is null)
        {
            return false;
        }

        try
        {
            parts = new PhcParts(
                memory.Value,
                iterations.Value,
                parallelism.Value,
                Convert.FromBase64String(segments[3]),
                Convert.FromBase64String(segments[4]));
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private readonly record struct PhcParts(int Memory, int Iterations, int Parallelism, byte[] Salt, byte[] Hash);
}
