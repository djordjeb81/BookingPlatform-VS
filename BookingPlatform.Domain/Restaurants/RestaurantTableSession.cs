using BookingPlatform.Domain.Businesses;
using BookingPlatform.Domain.Common;
using BookingPlatform.Domain.Resources;

namespace BookingPlatform.Domain.Restaurants;

public sealed class RestaurantTableSession : AuditableEntity
{
    public long BusinessId { get; set; }

    public Business Business { get; set; } = null!;

    public long RestaurantAreaId { get; set; }

    public RestaurantArea RestaurantArea { get; set; } = null!;

    public long TableResourceId { get; set; }

    public Resource TableResource { get; set; } = null!;

    public int? PartySize { get; set; }

    public string? CustomerName { get; set; }

    public string? CustomerPhone { get; set; }

    public string? Note { get; set; }

    public RestaurantTableSessionStatus Status { get; set; } = RestaurantTableSessionStatus.Active;

    public DateTime StartedAtUtc { get; set; }

    public DateTime? ReleasedAtUtc { get; set; }
}