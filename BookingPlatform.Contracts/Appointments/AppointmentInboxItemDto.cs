namespace BookingPlatform.Contracts.Appointments;

public sealed class AppointmentInboxItemDto
{
    public long AppointmentId { get; set; }
    public long? ChangeRequestId { get; set; }

    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;

    public long ServiceId { get; set; }
    public string ServiceName { get; set; } = string.Empty;

    public long? StaffMemberId { get; set; }
    public string? StaffDisplayName { get; set; }

    public string AppointmentStatus { get; set; } = string.Empty;
    public string? ChangeRequestType { get; set; }
    public string? ChangeRequestStatus { get; set; }

    public DateTime StartAtUtc { get; set; }
    public DateTime EndAtUtc { get; set; }

    public DateTime? ProposedStartAtUtc { get; set; }
    public DateTime? ProposedEndAtUtc { get; set; }

    public string? Message { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public string? LastOwnerAction { get; set; }
    public DateTime? LastOwnerActionAtUtc { get; set; }
    public string? LastOwnerActionLabel { get; set; }
    public bool RequiresOwnerFollowUp { get; set; }
    public string? FollowUpHint { get; set; }
    public string? OwnerWorkflowState { get; set; }
    public string? OwnerWorkflowLabel { get; set; }
    public DateTime? ScheduledCallAttemptAtUtc { get; set; }
}