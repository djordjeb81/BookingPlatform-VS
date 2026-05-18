namespace BookingPlatform.Contracts.Restaurants;

public sealed class RestaurantOrderMessageDto
{
    public long Id { get; set; }

    public long BusinessId { get; set; }

    public long? OrderId { get; set; }

    public int SenderType { get; set; }

    public string SenderTypeText { get; set; } = "";

    public int MessageType { get; set; }

    public string MessageTypeText { get; set; } = "";

    public string Text { get; set; } = "";

    public string? ActionKey { get; set; }

    public bool IsActionRequired { get; set; }

    public bool IsActionCompleted { get; set; }

    public DateTime? ActionCompletedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public long? SenderOperationUnitId { get; set; }

    public List<long> RecipientOperationUnitIds { get; set; } = new();
}