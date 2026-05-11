namespace BookingPlatform.Contracts.Push;

public sealed class UnregisterPushTokenRequest
{
    public string Token { get; set; } = string.Empty;
}