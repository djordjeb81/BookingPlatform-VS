using BookingPlatform.Contracts.Businesses;

namespace BookingPlatform.Contracts.CustomerPortal;

public sealed class CustomerPortalBusinessDto
{
    public long BusinessId { get; set; }

    public string BusinessName { get; set; } = string.Empty;

    public long BusinessCustomerId { get; set; }

    public long CustomerProfileId { get; set; }

    public string CustomerName { get; set; } = string.Empty;

    public int BusinessType { get; set; }

    public int BookingMode { get; set; }

    public BusinessFeatureSettingsDto FeatureSettings { get; set; } = new();

    public string? BusinessPhone { get; set; }

    public string? BusinessEmail { get; set; }

    public string? City { get; set; }
}