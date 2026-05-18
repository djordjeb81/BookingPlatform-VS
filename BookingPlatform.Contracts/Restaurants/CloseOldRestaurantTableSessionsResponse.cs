namespace BookingPlatform.Contracts.Restaurants;

public sealed class CloseOldRestaurantTableSessionsResponse
{
    public long BusinessId { get; set; }

    public long? RestaurantAreaId { get; set; }

    public int ClosedCount { get; set; }

    public int SkippedCount { get; set; }

    public DateTime ClosedAtUtc { get; set; }

    public string Message { get; set; } = string.Empty;

    public List<CloseOldRestaurantTableSessionSkippedDto> SkippedSessions { get; set; } = new();
}

public sealed class CloseOldRestaurantTableSessionSkippedDto
{
    public long SessionId { get; set; }

    public long TableResourceId { get; set; }

    public string? TableName { get; set; }

    public string Reason { get; set; } = string.Empty;
}