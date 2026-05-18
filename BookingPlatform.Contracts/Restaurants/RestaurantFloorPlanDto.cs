namespace BookingPlatform.Contracts.Restaurants;

public sealed class RestaurantFloorPlanDto
{
    public long BusinessId { get; set; }

    public long RestaurantAreaId { get; set; }

    public string AreaName { get; set; } = string.Empty;

    public int CanvasWidth { get; set; }

    public int CanvasHeight { get; set; }

    public string? BoundaryPointsJson { get; set; }

    public bool IsAreaActive { get; set; }

    public bool IsReservableAsWhole { get; set; }

    public long? WholeAreaResourceId { get; set; }

    public int AreaStatus { get; set; }

    public string AreaStatusText { get; set; } = string.Empty;

    public DateTime StatusAtUtc { get; set; }

    public long? CurrentAreaReservationId { get; set; }

    public DateTime? CurrentAreaReservationStartedAtUtc { get; set; }

    public DateTime? CurrentAreaReservationEndsAtUtc { get; set; }

    public string? CurrentAreaReservationCustomerName { get; set; }

    public int? CurrentAreaReservationPartySize { get; set; }

    public long? NextAreaReservationId { get; set; }

    public DateTime? NextAreaReservationAtUtc { get; set; }

    public string? NextAreaReservationCustomerName { get; set; }

    public int? NextAreaReservationPartySize { get; set; }

    public string? AreaReservationWarningText { get; set; }

    public List<RestaurantAreaReservationSummaryDto> UpcomingAreaReservations { get; set; } = new();

    public int UnassignedUpcomingReservationCount { get; set; }

    public DateTime? NextUnassignedReservationAtUtc { get; set; }

    public List<RestaurantUnassignedReservationDto> UnassignedUpcomingReservations { get; set; } = new();

    public List<RestaurantLayoutElementDto> Elements { get; set; } = new();

    public List<RestaurantFloorPlanResourceDto> Resources { get; set; } = new();
}

public sealed class RestaurantFloorPlanResourceDto
{
    public long ResourceId { get; set; }

    public long BusinessId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int ResourceType { get; set; }

    public int? Capacity { get; set; }

    public bool IsActive { get; set; }

    public long? ResourceGroupId { get; set; }

    public long? RestaurantAreaId { get; set; }

    public decimal? LayoutX { get; set; }

    public decimal? LayoutY { get; set; }

    public decimal? LayoutWidth { get; set; }

    public decimal? LayoutHeight { get; set; }

    public int LayoutRotationDeg { get; set; }

    public int LayoutShape { get; set; }

    public string? LayoutPointsJson { get; set; }

    public int Status { get; set; }

    public string StatusText { get; set; } = string.Empty;

    public long? CurrentTableSessionId { get; set; }

    public long? CurrentAppointmentId { get; set; }

    public DateTime? OccupiedFromUtc { get; set; }

    public List<RestaurantTableReservationPreviewDto> UpcomingReservations { get; set; } = new();

    public int? PartySize { get; set; }

    public string? CustomerName { get; set; }

    public DateTime? NextReservationAtUtc { get; set; }
    public int ActiveOrderCount { get; set; }

    public bool HasActiveOrders { get; set; }

    public decimal ActiveOrderTotalAmount { get; set; }

    public int? LatestOrderStatus { get; set; }

    public string? LatestOrderStatusText { get; set; }
    public decimal BillTotalAmount { get; set; }

    public decimal BillPaidAmount { get; set; }

    public decimal BillRemainingAmount { get; set; }

    public bool IsBillFullyPaid { get; set; }
    public long? NextReservationId { get; set; }

    public string? NextReservationCustomerName { get; set; }

    public int? NextReservationPartySize { get; set; }

    public string? ReservationWarningText { get; set; }

    public DateTime? MustBeFreeByUtc { get; set; }
}