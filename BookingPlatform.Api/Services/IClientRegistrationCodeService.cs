namespace BookingPlatform.Api.Services;

public interface IClientRegistrationCodeService
{
    Task<ClientRegistrationCodeResult> CreateCodeAsync(
        string email,
        string purpose,
        CancellationToken cancellationToken);

    Task<bool> VerifyCodeAsync(
        string email,
        string code,
        string purpose,
        CancellationToken cancellationToken);
}

public static class EmailVerificationPurposes
{
    public const string ClientRegister = "client_register";
    public const string PasswordReset = "password_reset";
}

public sealed class ClientRegistrationCodeResult
{
    public string Email { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public DateTime ExpiresAtUtc { get; set; }
}