namespace Sodalis.Modules.Identity.AuthProviders;

public sealed record AuthResult(
    bool Success,
    string? ExternalId,
    string? Email = null,
    string? DisplayName = null,
    string? FailureReason = null)
{
    public static AuthResult Ok(string externalId, string? email = null, string? displayName = null) =>
        new(true, externalId, email, displayName);

    public static AuthResult Fail(string reason) =>
        new(false, null, FailureReason: reason);
}
