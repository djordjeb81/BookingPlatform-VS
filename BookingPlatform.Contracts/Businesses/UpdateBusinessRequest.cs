namespace BookingPlatform.Contracts.Businesses;

public sealed class UpdateBusinessRequest
{
    public string Name { get; set; } = string.Empty;
    public int BusinessType { get; set; }
    public string? Description { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }

    public string? Street { get; set; }
    public string? StreetNumber { get; set; }
    public string? City { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? GooglePlaceId { get; set; }

    public int SlotIntervalMin { get; set; }
    public bool IsActive { get; set; }
}