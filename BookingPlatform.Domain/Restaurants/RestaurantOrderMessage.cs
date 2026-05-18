using BookingPlatform.Domain.Common;

namespace BookingPlatform.Domain.Restaurants;

public sealed class RestaurantOrderMessage : AuditableEntity
{
    public long BusinessId { get; set; }

    public long? OrderId { get; set; }

    public RestaurantOrder? Order { get; set; }

    public RestaurantOrderMessageSenderType SenderType { get; set; } =
        RestaurantOrderMessageSenderType.System;

    public long? SenderOperationUnitId { get; set; }

    public RestaurantOrderMessageType MessageType { get; set; } =
        RestaurantOrderMessageType.Text;

    public string Text { get; set; } = "";

    public string? ActionKey { get; set; }

    public bool IsActionRequired { get; set; }

    public bool IsActionCompleted { get; set; }

    public DateTime? ActionCompletedAtUtc { get; set; }

    public ICollection<RestaurantOrderMessageRecipient> Recipients { get; set; } =
        new List<RestaurantOrderMessageRecipient>();
}