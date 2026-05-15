using System.Text.Json;

namespace Sodalis.Modules.Identity.AuthProviders;

public sealed record AuthRequest(
    string ProviderId,
    JsonElement Payload,
    Guid GameId);
