namespace BookingPlatform.Contracts.Restaurants;

public sealed class RestaurantTableSessionDto
{
    public long Id { get; set; }

    public long BusinessId { get; set; }

    public long RestaurantAreaId { get; set; }

    public long TableResourceId { get; set; }

    public string TableName { get; set; } = string.Empty;

    public int? PartySize { get; set; }

    public string? CustomerName { get; set; }

    public string? CustomerPhone { get; set; }

    public string? Note { get; set; }

    public int Status { get; set; }

    public string StatusText { get; set; } = string.Empty;

    public DateTime StartedAtUtc { get; set; }

    public DateTime? ReleasedAtUtc { get; set; }
}