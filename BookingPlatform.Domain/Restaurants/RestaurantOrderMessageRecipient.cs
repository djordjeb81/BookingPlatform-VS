using BookingPlatform.Domain.Common;

namespace BookingPlatform.Domain.Restaurants;

public sealed class RestaurantOrderMessageRecipient : AuditableEntity
{
    public long BusinessId { get; set; }

    public long MessageId { get; set; }

    public RestaurantOrderMessage Message { get; set; } = null!;

    public RestaurantOrderMessageRecipientType RecipientType { get; set; } =
        RestaurantOrderMessageRecipientType.OperationUnit;

    public long? RecipientOperationUnitId { get; set; }

    public bool IsRead { get; set; }

    public DateTime? ReadAtUtc { get; set; }
}