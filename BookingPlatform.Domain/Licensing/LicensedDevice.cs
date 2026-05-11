using System;

namespace BookingPlatform.Domain.Licensing;

public sealed class LicensedDevice
{
    public long Id { get; set; }

    public long AppUserId { get; set; }

    public string HwidHash { get; set; } = string.Empty;
    public string ComputerName { get; set; } = string.Empty;
    public string ProgramVersion { get; set; } = string.Empty;

    public DeviceLicenseStatus Status { get; set; } = DeviceLicenseStatus.Pending;

    public string? LicenseToken { get; set; }
    public DateTime? LicenseTokenIssuedAtUtc { get; set; }
    public DateTime? ValidUntilUtc { get; set; }

    public DateTime? LastSeenAtUtc { get; set; }
    public DateTime? LastLicenseRefreshAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}