namespace BookingPlatform.Contracts.Restaurants;

public sealed class UpdateRestaurantOperationUnitRequestDto
{
    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public int DisplayOrder { get; set; }

    public bool ReceivesCustomerChat { get; set; }
}