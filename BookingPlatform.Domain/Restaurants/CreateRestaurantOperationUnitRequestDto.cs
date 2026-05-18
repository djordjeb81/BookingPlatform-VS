namespace BookingPlatform.Contracts.Restaurants;

public sealed class CreateRestaurantOperationUnitRequestDto
{
    public long BusinessId { get; set; }

    public int UnitType { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public int DisplayOrder { get; set; }

    public bool ReceivesCustomerChat { get; set; }
}