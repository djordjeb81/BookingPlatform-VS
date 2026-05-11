namespace BookingPlatform.Contracts.License;

public sealed class RegisterDeviceRequest
{
    public string HwidHash { get; set; } = string.Empty;
    public string ComputerName { get; set; } = string.Empty;
    public string ProgramVersion { get; set; } = string.Empty;
}