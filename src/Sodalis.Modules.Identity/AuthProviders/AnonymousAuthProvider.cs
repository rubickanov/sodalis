namespace Sodalis.Modules.Identity.AuthProviders;

public sealed class AnonymousAuthProvider : IAuthProvider
{
    public string ProviderId => "anonymous";

    public Task<AuthResult> AuthenticateAsync(AuthRequest request, CancellationToken ct)
    {
        // Anonymous: payload is empty / ignored. Server-generated identity.
        var externalId = Guid.NewGuid().ToString("N");
        return Task.FromResult(AuthResult.Ok(externalId));
    }
}
