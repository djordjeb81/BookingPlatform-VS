using BookingPlatform.Domain.Auth;
using BookingPlatform.Domain.Common;

namespace BookingPlatform.Domain.Businesses;

public sealed class Business : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public BusinessType BusinessType { get; set; }

    public BookingMode BookingMode { get; set; } = BookingMode.ServiceAppointment;

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

    public bool IsActive { get; set; } = true;

    // Na koliko minuta od početka radnog vremena kreću mogući počeci termina.
    // Primer: 30 => 09:00, 09:30, 10:00...
    public int SlotIntervalMin { get; set; } = 30;

    public ICollection<BusinessUserMembership> UserMemberships { get; set; } =
        new List<BusinessUserMembership>();

    public BusinessFeatureSettings? FeatureSettings { get; set; }
}