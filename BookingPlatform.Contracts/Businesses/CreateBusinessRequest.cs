namespace BookingPlatform.Contracts.Businesses;

public sealed class CreateBusinessRequest
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

    // Na koliko minuta od početka radnog vremena mogu da počinju termini.
    // Primer: 30 => termini kreću na :00 i :30 od početka smene.
    public int SlotIntervalMin { get; set; } = 30;
}