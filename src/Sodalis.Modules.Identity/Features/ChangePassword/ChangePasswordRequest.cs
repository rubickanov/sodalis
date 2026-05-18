namespace Sodalis.Modules.Identity.Features.ChangePassword;

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
