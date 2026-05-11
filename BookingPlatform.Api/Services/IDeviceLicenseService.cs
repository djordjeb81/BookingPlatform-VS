using BookingPlatform.Domain.Licensing;

namespace BookingPlatform.Api.Services;

public interface IDeviceLicenseService
{
    Task<RegisterDeviceResult> RegisterDeviceAsync(
        long appUserId,
        string hwidHash,
        string computerName,
        string programVersion,
        CancellationToken cancellationToken);

    Task<RefreshLicenseResult> RefreshAsync(
        long appUserId,
        string hwidHash,
        string computerName,
        string programVersion,
        CancellationToken cancellationToken);

    Task<LicenseStatusResult> GetStatusAsync(
        long appUserId,
        string hwidHash,
        CancellationToken cancellationToken);
}

public sealed class RegisterDeviceResult
{
    public bool Succeeded { get; set; }
    public bool NotFound { get; set; }

    public string ErrorMessage { get; set; } = string.Empty;
    public string ErrorReasonCode { get; set; } = string.Empty;

    public long DeviceId { get; set; }
    public DeviceLicenseStatus Status { get; set; }
    public string HwidHash { get; set; } = string.Empty;
    public string ComputerName { get; set; } = string.Empty;
    public string ProgramVersion { get; set; } = string.Empty;
    public DateTime? LastSeenAtUtc { get; set; }
    public DateTime? ValidUntilUtc { get; set; }
    public string Message { get; set; } = string.Empty;
}

public sealed class RefreshLicenseResult
{
    public bool Succeeded { get; set; }
    public bool NotFound { get; set; }

    public string ErrorMessage { get; set; } = string.Empty;
    public string ErrorReasonCode { get; set; } = string.Empty;

    public long DeviceId { get; set; }
    public DeviceLicenseStatus Status { get; set; }
    public bool IsApproved { get; set; }

    public string? LicenseToken { get; set; }
    public DateTime? ValidUntilUtc { get; set; }
    public DateTime? LastSeenAtUtc { get; set; }
    public DateTime? LastLicenseRefreshAtUtc { get; set; }

    public string Message { get; set; } = string.Empty;
}

public sealed class LicenseStatusResult
{
    public bool Succeeded { get; set; }
    public bool NotFound { get; set; }

    public string ErrorMessage { get; set; } = string.Empty;
    public string ErrorReasonCode { get; set; } = string.Empty;

    public long DeviceId { get; set; }
    public DeviceLicenseStatus Status { get; set; }
    public bool IsApproved { get; set; }

    public string HwidHash { get; set; } = string.Empty;
    public string ComputerName { get; set; } = string.Empty;
    public string ProgramVersion { get; set; } = string.Empty;

    public DateTime? LastSeenAtUtc { get; set; }
    public DateTime? LastLicenseRefreshAtUtc { get; set; }
    public DateTime? ValidUntilUtc { get; set; }

    public string Message { get; set; } = string.Empty;
}