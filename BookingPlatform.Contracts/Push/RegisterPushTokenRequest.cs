namespace BookingPlatform.Contracts.Push;

public sealed class RegisterPushTokenRequest
{
    public string Token { get; set; } = string.Empty;

    public string Platform { get; set; } = "Android";

    public string? DeviceName { get; set; }
}