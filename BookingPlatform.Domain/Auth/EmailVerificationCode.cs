namespace BookingPlatform.Domain.Auth;

public sealed class EmailVerificationCode
{
    public long Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string NormalizedEmail { get; set; } = string.Empty;

    public string CodeHash { get; set; } = string.Empty;

    public string Purpose { get; set; } = string.Empty;

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime? UsedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}