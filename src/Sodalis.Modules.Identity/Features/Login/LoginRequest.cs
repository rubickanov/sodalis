using System.Text.Json;

namespace Sodalis.Modules.Identity.Features.Login;

public sealed record LoginRequest(string Provider, JsonElement Payload);
