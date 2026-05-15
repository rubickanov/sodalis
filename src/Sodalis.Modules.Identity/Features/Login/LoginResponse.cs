namespace Sodalis.Modules.Identity.Features.Login;

public sealed record LoginResponse(
    string AccessToken,
    int ExpiresIn,
    string TokenType,
    PlayerInfo Player);

public sealed record PlayerInfo(
    Guid PlayerId,
    bool IsNew,
    IReadOnlyList<string> LinkedProviders);
