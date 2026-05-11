namespace BookingPlatform.Contracts.BusinessActivities;

public sealed class BusinessActivityItemDto
{
    public string ActivityType { get; set; } = "";
    // NewAppointmentRequest
    // AppointmentChangeRequest
    // ChatMessage - kasnije

    public string ActivityTypeLabel { get; set; } = "";

    public long BusinessId { get; set; }

    public long? AppointmentId { get; set; }
    public long? ChangeRequestId { get; set; }
    public long? BusinessCustomerId { get; set; }
    public long? ConversationId { get; set; }
    public long? ChatMessageId { get; set; }

    public string CustomerName { get; set; } = "";
    public string? CustomerPhone { get; set; }

    public long? ServiceId { get; set; }
    public string? ServiceName { get; set; }

    public long? StaffMemberId { get; set; }
    public string? StaffDisplayName { get; set; }

    public long? ResourceId { get; set; }
    public string? ResourceName { get; set; }

    public string Title { get; set; } = "";
    public string PreviewText { get; set; } = "";

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? StartAtUtc { get; set; }
    public DateTime? EndAtUtc { get; set; }

    public DateTime? ProposedStartAtUtc { get; set; }
    public DateTime? ProposedEndAtUtc { get; set; }

    public DateTime? ExpiresAtUtc { get; set; }

    public bool IsUnread { get; set; }
    public bool RequiresAction { get; set; }

    public string PrimaryAction { get; set; } = "";
    // OpenRequest
    // OpenChat - kasnije
    // OpenAppointment
}