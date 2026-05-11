using System;

namespace BookingPlatform.Contracts.License;

public sealed class RegisterDeviceResponse
{
    public long DeviceId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    public string HwidHash { get; set; } = string.Empty;
    public string ComputerName { get; set; } = string.Empty;
    public string ProgramVersion { get; set; } = string.Empty;

    public DateTime? LastSeenAtUtc { get; set; }
    public DateTime? ValidUntilUtc { get; set; }
}