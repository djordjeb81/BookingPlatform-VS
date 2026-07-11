using BookingPlatform.Domain.Businesses;
using BookingPlatform.Domain.Common;

namespace BookingPlatform.Domain.BusinessActivityNotifications;

public sealed class BusinessActivityNotification : Entity
{
    public long BusinessId { get; set; }

    public Business Business { get; set; } = null!;

    public string RecipientType { get; set; } = BusinessActivityNotificationRecipients.Business;

    public string RecipientKey { get; set; } = "business";

    public long? RecipientAppUserId { get; set; }

    public long? RecipientCustomerProfileId { get; set; }

    public long? RecipientStaffMemberId { get; set; }

    public long? RecipientOperationUnitId { get; set; }

    public string Domain { get; set; } = string.Empty;

    public string Kind { get; set; } = string.Empty;

    public string ActivityKey { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string MainText { get; set; } = string.Empty;

    public string PreviewText { get; set; } = string.Empty;

    public int Priority { get; set; }

    public bool IsSeen { get; set; }

    public DateTime? SeenAtUtc { get; set; }

    public long? SeenByUserId { get; set; }

    public bool IsResolved { get; set; }

    public DateTime? ResolvedAtUtc { get; set; }

    public long? ResolvedByUserId { get; set; }

    public DateTime? SnoozedUntilUtc { get; set; }

    public long? SnoozedByUserId { get; set; }

    public DateTime? LastReminderAtUtc { get; set; }

    public DateTime SortAtUtc { get; set; }

    public long? AppointmentId { get; set; }

    public long? ChangeRequestId { get; set; }

    public long? RestaurantOrderId { get; set; }

    public long? RestaurantTableReservationId { get; set; }

    public long? RestaurantAreaReservationId { get; set; }

    public long? ConversationId { get; set; }

    public long? ChatMessageId { get; set; }

    public long? SystemAlarmTriggerId { get; set; }

    public long? FitnessSessionId { get; set; }

    public long? FitnessSessionBookingId { get; set; }

    public long? FitnessMemberId { get; set; }

    public long? FitnessMemberSessionDebtId { get; set; }

    public long? CustomerProfileId { get; set; }

    public long? BusinessCustomerId { get; set; }

    public string? CustomerName { get; set; }

    public string? CustomerPhone { get; set; }

    public string? PayloadJson { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}