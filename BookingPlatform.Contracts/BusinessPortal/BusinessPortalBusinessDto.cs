using BookingPlatform.Contracts.Businesses;

namespace BookingPlatform.Contracts.BusinessPortal;

public sealed class BusinessPortalBusinessDto
{
    public long BusinessId { get; set; }

    public string BusinessName { get; set; } = string.Empty;

    public int BusinessType { get; set; }

    public int BookingMode { get; set; }

    public BusinessFeatureSettingsDto FeatureSettings { get; set; } = new();

    public string Role { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? City { get; set; }
}