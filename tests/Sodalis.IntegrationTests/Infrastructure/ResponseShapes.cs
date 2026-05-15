namespace Sodalis.IntegrationTests.Infrastructure;

// Lightweight response shapes used for deserialization in tests.
// We don't reference the production DTOs to keep tests insulated
// from internal renames and to assert the wire contract explicitly.

public sealed record LoginLikeResponse(
    string AccessToken,
    int ExpiresIn,
    string RefreshToken,
    int RefreshTokenExpiresIn,
    string TokenType,
    PlayerInfoDto Player);

public sealed record PlayerInfoDto(
    Guid PlayerId,
    bool IsNew,
    IReadOnlyList<string> LinkedProviders);

public sealed record ValidationProblem(
    string Type,
    string Title,
    int Status,
    Dictionary<string, string[]> Errors);
