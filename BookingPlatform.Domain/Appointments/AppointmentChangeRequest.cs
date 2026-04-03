using BookingPlatform.Domain.Common;

namespace BookingPlatform.Domain.Appointments;

public sealed class AppointmentChangeRequest : AuditableEntity
{
    public long AppointmentId { get; set; }

    public AppointmentChangeRequestType RequestType { get; set; }
    public AppointmentChangeRequestStatus Status { get; set; }
    public ChangeInitiatorType InitiatedBy { get; set; }

    public DateTime OriginalStartAtUtc { get; set; }
    public DateTime OriginalEndAtUtc { get; set; }

    public DateTime ProposedStartAtUtc { get; set; }
    public DateTime ProposedEndAtUtc { get; set; }

    public string? Reason { get; set; }
    public string? Message { get; set; }

    public DateTime? ExpiresAtUtc { get; set; }
    public DateTime? RespondedAtUtc { get; set; }
}