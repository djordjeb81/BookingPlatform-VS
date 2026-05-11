using BookingPlatform.Domain.Common;

namespace BookingPlatform.Domain.Push;

public sealed class UserPushToken : AuditableEntity
{
    public long AppUserId { get; set; }

    public string Token { get; set; } = string.Empty;

    public string Platform { get; set; } = "Android";

    public string? DeviceName { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime LastSeenAtUtc { get; set; }
}