namespace BookingPlatform.Contracts.Restaurants;

public sealed class RestaurantOperationUnitDto
{
    public long Id { get; set; }

    public long BusinessId { get; set; }

    public int UnitType { get; set; }

    public string UnitTypeText { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public int DisplayOrder { get; set; }

    public bool ReceivesCustomerChat { get; set; }

    public List<RestaurantOperationUnitWorkingHourDto> WorkingHours { get; set; } = new();
}