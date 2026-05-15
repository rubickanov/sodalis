using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sodalis.Modules.Identity.Auth;
using Sodalis.Modules.Identity.Persistence;

namespace Sodalis.Modules.Identity.AuthProviders;

public sealed class EmailPasswordAuthProvider(IdentityDbContext db, PasswordHasher hasher) : IAuthProvider
{
    public const string Id = "email";

    // Reject inputs longer than these BEFORE running Argon2id verify.
    // Without this, an attacker can send a 10MB password and cost the server
    // ~30MB memory + significant CPU per request — easy DoS vector.
    private const int MaxEmailLength = 254;
    private const int MaxPasswordLength = 256;

    public string ProviderId => Id;

    public async Task<AuthResult> AuthenticateAsync(AuthRequest request, CancellationToken ct)
    {
        if (!request.Payload.TryGetProperty("email", out var emailEl)
            || !request.Payload.TryGetProperty("password", out var passwordEl)
            || emailEl.ValueKind != JsonValueKind.String
            || passwordEl.ValueKind != JsonValueKind.String)
        {
            return AuthResult.Fail("Email and password are required.");
        }

        var rawEmail = emailEl.GetString()!;
        var password = passwordEl.GetString()!;

        // Anti-enumeration + DoS guard: same generic error as wrong creds.
        if (rawEmail.Length > MaxEmailLength || password.Length > MaxPasswordLength)
        {
            return AuthResult.Fail("Invalid email or password.");
        }

        var email = NormalizeEmail(rawEmail);

        var identity = await db.ExternalIdentities
            .FirstOrDefaultAsync(
                ei => ei.GameId == request.GameId
                      && ei.ProviderId == Id
                      && ei.ExternalId == email,
                ct);

        if (identity?.Metadata is null)
        {
            return AuthResult.Fail("Invalid email or password.");
        }

        var meta = JsonSerializer.Deserialize<EmailMetadata>(identity.Metadata);
        if (meta?.PasswordHash is null || !hasher.Verify(password, meta.PasswordHash))
        {
            return AuthResult.Fail("Invalid email or password.");
        }

        return AuthResult.Ok(email, email: email);
    }

    public static string NormalizeEmail(string raw) => raw.Trim().ToLowerInvariant();
}

public sealed record EmailMetadata(
    string PasswordHash,
    bool EmailVerified,
    DateTimeOffset? EmailVerifiedAt = null);
