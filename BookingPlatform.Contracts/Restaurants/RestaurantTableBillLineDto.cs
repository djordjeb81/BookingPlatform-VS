namespace BookingPlatform.Contracts.Restaurants;

public sealed class RestaurantTableBillLineDto
{
    public string Name { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal LineTotal { get; set; }

    public string? OptionsText { get; set; }

    public string? Note { get; set; }
}