namespace BookingPlatform.Contracts.BusinessActivities;

public sealed class BusinessActivitySummaryDto
{
    public long BusinessId { get; set; }

    public int UnreadCount { get; set; }

    public bool HasUnread => UnreadCount > 0;

    public BusinessActivityItemDto? LatestActivity { get; set; }

    public List<BusinessActivityItemDto> LatestItems { get; set; } = new();
}