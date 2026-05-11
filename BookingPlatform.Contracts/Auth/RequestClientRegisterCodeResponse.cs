namespace BookingPlatform.Contracts.Auth;

public sealed class RequestClientRegisterCodeResponse
{
    public string Email { get; set; } = string.Empty;

    public bool ExistingAppUserFound { get; set; }

    public bool ExistingCustomerProfileFound { get; set; }

    public string Message { get; set; } = string.Empty;

    // Samo za development/test dok ne uvedemo pravo slanje email-a.
    public string? DevelopmentCode { get; set; }
}