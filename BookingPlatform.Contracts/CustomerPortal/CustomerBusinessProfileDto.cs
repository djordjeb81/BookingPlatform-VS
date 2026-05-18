using BookingPlatform.Contracts.Businesses;

namespace BookingPlatform.Contracts.CustomerPortal;

public sealed class CustomerBusinessProfileDto
{
    public long BusinessId { get; set; }

    public string BusinessName { get; set; } = string.Empty;

    public string? BusinessPhone { get; set; }

    public string? BusinessEmail { get; set; }

    public string? Street { get; set; }

    public string? StreetNumber { get; set; }

    public string? City { get; set; }

    public string? PostalCode { get; set; }

    public string? Country { get; set; }

    public int BusinessType { get; set; }

    public int BookingMode { get; set; }

    public BusinessFeatureSettingsDto FeatureSettings { get; set; } = new();

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public bool IsAlreadyConnected { get; set; }

    public long? BusinessCustomerId { get; set; }

    public List<string> Services { get; set; } = new();
}