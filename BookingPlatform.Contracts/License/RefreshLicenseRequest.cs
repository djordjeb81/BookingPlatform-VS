namespace BookingPlatform.Contracts.License;

public sealed class RefreshLicenseRequest
{
    public string HwidHash { get; set; } = string.Empty;
    public string ComputerName { get; set; } = string.Empty;
    public string ProgramVersion { get; set; } = string.Empty;
}