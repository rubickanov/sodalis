namespace Sodalis.Modules.Identity.Features.ResetPassword;

public sealed record ResetPasswordRequest(string Token, string NewPassword);
