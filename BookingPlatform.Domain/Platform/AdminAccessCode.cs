namespace BookingPlatform.Domain.Platform;

public sealed class AdminAccessCode
{
    public long Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string CodeHash { get; set; } = string.Empty;

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime? UsedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public string? CreatedFromIp { get; set; }
}