namespace BookingPlatform.Contracts.Auth;

public sealed class RequestPasswordResetCodeRequest
{
    public string Email { get; set; } = string.Empty;
}