namespace BookingPlatform.Contracts.Restaurants;

public sealed class RestaurantDashboardSummaryDto
{
    public long BusinessId { get; set; }

    public DateTime StatusAtUtc { get; set; }

    public DateTime TodayFromUtc { get; set; }

    public DateTime TomorrowFromUtc { get; set; }

    public int ActiveTableSessionCount { get; set; }

    public int PendingTableReservationCount { get; set; }

    public int ConfirmedTableReservationTodayCount { get; set; }

    public int UnassignedConfirmedTableReservationTodayCount { get; set; }

    public int ActiveKitchenOrderCount { get; set; }

    public int TodayTakeawayOrderCount { get; set; }

    public int TodayDeliveryOrderCount { get; set; }

    public int PendingAreaReservationCount { get; set; }

    public int ConfirmedAreaReservationTodayCount { get; set; }

    public int ActiveAreaReservationCount { get; set; }

    public decimal TodayOrderTotalAmount { get; set; }

    public string Currency { get; set; } = "RSD";
}