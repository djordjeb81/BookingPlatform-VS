namespace BookingPlatform.Domain.Platform;

public sealed class AdminAccessSession
{
    public long Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public string? CreatedFromIp { get; set; }
}