namespace BookingPlatform.Contracts.BusinessActivityNotifications;

public sealed class BusinessActivityNotificationDto
{
    public long Id { get; set; }

    public long BusinessId { get; set; }

    public string RecipientType { get; set; } = "Business";

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

    public bool IsResolved { get; set; }

    public DateTime? ResolvedAtUtc { get; set; }

    public DateTime? SnoozedUntilUtc { get; set; }

    public DateTime? LastReminderAtUtc { get; set; }

    public DateTime SortAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

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

    public bool IsSnoozed =>
        SnoozedUntilUtc.HasValue &&
        SnoozedUntilUtc.Value > DateTime.UtcNow;

    public string StatusText
    {
        get
        {
            if (IsResolved)
                return "Rešeno";

            if (IsSnoozed)
                return "Odloženo";

            if (IsSeen)
                return "Viđeno";

            return "Novo";
        }
    }
}