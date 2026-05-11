using System;

namespace BookingPlatform.Contracts.License;

public sealed class LicenseStatusResponse
{
    public long DeviceId { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsApproved { get; set; }

    public string HwidHash { get; set; } = string.Empty;
    public string ComputerName { get; set; } = string.Empty;
    public string ProgramVersion { get; set; } = string.Empty;

    public DateTime? LastSeenAtUtc { get; set; }
    public DateTime? LastLicenseRefreshAtUtc { get; set; }
    public DateTime? ValidUntilUtc { get; set; }

    public string Message { get; set; } = string.Empty;
}