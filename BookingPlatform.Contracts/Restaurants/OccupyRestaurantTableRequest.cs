namespace BookingPlatform.Contracts.Restaurants;

public sealed class OccupyRestaurantTableRequest
{
    public long BusinessId { get; set; }

    public long RestaurantAreaId { get; set; }

    public long TableResourceId { get; set; }

    public int? PartySize { get; set; }

    public string? CustomerName { get; set; }

    public string? CustomerPhone { get; set; }

    public string? Note { get; set; }
}