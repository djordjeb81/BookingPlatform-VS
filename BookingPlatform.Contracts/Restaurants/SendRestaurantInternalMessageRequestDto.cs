namespace BookingPlatform.Contracts.Restaurants;

public sealed class SendRestaurantInternalMessageRequestDto
{
    public long BusinessId { get; set; }

    public long SenderOperationUnitId { get; set; }

    public long RecipientOperationUnitId { get; set; }

    public string Text { get; set; } = "";
}