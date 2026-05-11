namespace BookingPlatform.Contracts.Auth;

public sealed class RequestClientRegisterCodeRequest
{
    public string Email { get; set; } = string.Empty;
}