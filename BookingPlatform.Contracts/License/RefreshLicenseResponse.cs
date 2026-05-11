using System;

namespace BookingPlatform.Contracts.License;

public sealed class RefreshLicenseResponse
{
    public long DeviceId { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsApproved { get; set; }

    public string? LicenseToken { get; set; }
    public DateTime? ValidUntilUtc { get; set; }

    public DateTime? LastSeenAtUtc { get; set; }
    public DateTime? LastLicenseRefreshAtUtc { get; set; }

    public string Message { get; set; } = string.Empty;
}