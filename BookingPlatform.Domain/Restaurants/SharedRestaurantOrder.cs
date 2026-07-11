using BookingPlatform.Domain.Common;

namespace BookingPlatform.Domain.Restaurants;

public sealed class SharedRestaurantOrder : AuditableEntity
{
    public long OwnerCustomerProfileId { get; set; }

    public long? OwnerAppUserId { get; set; }

    public string OwnerDisplayNameSnapshot { get; set; } = string.Empty;

    public string? Title { get; set; }

    public string? Note { get; set; }

    public SharedRestaurantOrderStatus Status { get; set; } = SharedRestaurantOrderStatus.Draft;

    public DateTime? SentToChatAtUtc { get; set; }

    public DateTime? SubmittedAtUtc { get; set; }

    public ICollection<SharedRestaurantOrderItem> Items { get; set; } =
        new List<SharedRestaurantOrderItem>();
}
