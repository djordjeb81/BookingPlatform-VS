namespace BookingPlatform.Contracts.Auth;

public sealed class RegisterOwnerRequest
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    public string BusinessName { get; set; } = string.Empty;
    public int BusinessType { get; set; }
    public int BookingMode { get; set; } = 1;
    public string? Description { get; set; }
    public string? Phone { get; set; }
    public string? BusinessEmail { get; set; }
    public int SlotIntervalMin { get; set; } = 30;

    public string? Street { get; set; }
    public string? StreetNumber { get; set; }
    public string? City { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? GooglePlaceId { get; set; }
}