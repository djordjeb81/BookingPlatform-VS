namespace BookingPlatform.Contracts.BusinessActivityNotifications;

public sealed class BusinessActivityNotificationSummaryDto
{
    public long BusinessId { get; set; }

    public string RecipientType { get; set; } = string.Empty;

    public string RecipientKey { get; set; } = string.Empty;

    public int ActiveCount { get; set; }

    public int UnseenCount { get; set; }

    public int SnoozedCount { get; set; }

    public bool HasUnseen { get; set; }

    public BusinessActivityNotificationDto? LatestUnseen { get; set; }

    public List<BusinessActivityNotificationDto> LatestItems { get; set; } = new();
}