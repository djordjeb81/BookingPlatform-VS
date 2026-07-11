namespace BookingPlatform.Contracts.License;

public sealed class LicenseAdminDeviceDto
{
    public long Id { get; set; }

    public long AppUserId { get; set; }

    public string UserEmail { get; set; } = "";

    public string HwidHash { get; set; } = "";

    public string ComputerName { get; set; } = "";

    public string ProgramVersion { get; set; } = "";

    public string Status { get; set; } = "";

    public int StatusValue { get; set; }

    public string? LicenseToken { get; set; }

    public DateTime? ValidUntilUtc { get; set; }

    public DateTime? LastSeenAtUtc { get; set; }

    public DateTime? LastLicenseRefreshAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class LicenseAdminDeviceActionResponse
{
    public bool Succeeded { get; set; }

    public long DeviceId { get; set; }

    public string Status { get; set; } = "";

    public DateTime? ValidUntilUtc { get; set; }

    public string Message { get; set; } = "";
}

public sealed class ApproveLicenseDeviceRequest
{
    public DateTime? ValidUntilUtc { get; set; }
}

public sealed class ExtendLicenseDeviceRequest
{
    public DateTime ValidUntilUtc { get; set; }
}