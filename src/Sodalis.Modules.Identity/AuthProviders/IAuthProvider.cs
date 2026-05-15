namespace Sodalis.Modules.Identity.AuthProviders;

public interface IAuthProvider
{
    string ProviderId { get; }

    Task<AuthResult> AuthenticateAsync(AuthRequest request, CancellationToken ct);
}
