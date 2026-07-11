namespace BookingPlatform.Contracts.AdminAccess;

public sealed class RequestAdminAccessCodeRequest
{
    public string Email { get; set; } = string.Empty;
}

public sealed class RequestAdminAccessCodeResponse
{
    public bool Succeeded { get; set; }

    public string Message { get; set; } = string.Empty;
}

public sealed class VerifyAdminAccessCodeRequest
{
    public string Email { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;
}

public sealed class VerifyAdminAccessCodeResponse
{
    public bool IsAllowed { get; set; }

    public string AdminAccessToken { get; set; } = string.Empty;

    public DateTime? ExpiresAtUtc { get; set; }

    public string Message { get; set; } = string.Empty;
}