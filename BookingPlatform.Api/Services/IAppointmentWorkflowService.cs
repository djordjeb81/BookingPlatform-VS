using BookingPlatform.Domain.Appointments;

namespace BookingPlatform.Api.Services;

public interface IAppointmentWorkflowService
{
    Task<UpdateConfirmedAppointmentResult> CompleteConfirmedAsync(
        long appointmentId,
        string? note,
        CancellationToken cancellationToken);

    Task<UpdateConfirmedAppointmentResult> MarkNoShowAsync(
        long appointmentId,
        string? note,
        CancellationToken cancellationToken);

    Task<UpdateConfirmedAppointmentResult> CancelConfirmedAsync(
        long appointmentId,
        string? note,
        CancellationToken cancellationToken);

    Task<RescheduleDecisionResult> AcceptRescheduleRequestAsync(
        long appointmentId,
        long changeRequestId,
        CancellationToken cancellationToken);

    Task<RescheduleDecisionResult> RejectRescheduleRequestAsync(
        long appointmentId,
        long changeRequestId,
        string? reason,
        CancellationToken cancellationToken);

    Task<DelayProposalResult> ProposeDelayAsync(
        long appointmentId,
        int delayMinutes,
        string? message,
        CancellationToken cancellationToken);

    Task<ProposeTimeResult> ProposeTimeAsync(
        long appointmentId,
        DateTime proposedStartAtUtc,
        int? finalDurationMin,
        string? message,
        long? proposedStaffMemberId,
        CancellationToken cancellationToken);
}

public sealed class UpdateConfirmedAppointmentResult
{
    public bool Succeeded { get; set; }
    public bool NotFound { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public string ErrorReasonCode { get; set; } = string.Empty;

    public long AppointmentId { get; set; }
    public AppointmentStatus AppointmentStatus { get; set; }
    public DateTime StartAtUtc { get; set; }
    public DateTime EndAtUtc { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Note { get; set; }
}

public sealed class RescheduleDecisionResult
{
    public bool Succeeded { get; set; }
    public bool NotFound { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public string ErrorReasonCode { get; set; } = string.Empty;

    public long AppointmentId { get; set; }
    public AppointmentStatus AppointmentStatus { get; set; }
    public long ChangeRequestId { get; set; }
    public AppointmentChangeRequestStatus ChangeRequestStatus { get; set; }

    public DateTime StartAtUtc { get; set; }
    public DateTime EndAtUtc { get; set; }

    public string Message { get; set; } = string.Empty;

    public DateTime OriginalStartAtUtc { get; set; }
    public DateTime OriginalEndAtUtc { get; set; }

    public DateTime ProposedStartAtUtc { get; set; }
    public DateTime ProposedEndAtUtc { get; set; }

    public long? ProposedStaffMemberId { get; set; }

    public string? Reason { get; set; }
}

public sealed class DelayProposalResult
{
    public bool Succeeded { get; set; }
    public bool NotFound { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public string ErrorReasonCode { get; set; } = string.Empty;
    public bool HasAppointmentConflict { get; set; }

    public long AppointmentId { get; set; }
    public AppointmentStatus AppointmentStatus { get; set; }
    public long ChangeRequestId { get; set; }
    public AppointmentChangeRequestStatus ChangeRequestStatus { get; set; }
    public DateTime StartAtUtc { get; set; }
    public DateTime EndAtUtc { get; set; }
    public string Message { get; set; } = string.Empty;
}

public sealed class ProposeTimeResult
{
    public bool Succeeded { get; set; }
    public bool NotFound { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public string ErrorReasonCode { get; set; } = string.Empty;

    public long AppointmentId { get; set; }
    public AppointmentStatus AppointmentStatus { get; set; }

    public long ChangeRequestId { get; set; }
    public AppointmentChangeRequestStatus ChangeRequestStatus { get; set; }

    public DateTime StartAtUtc { get; set; }
    public DateTime EndAtUtc { get; set; }

    public long? ProposedStaffMemberId { get; set; }

    public int EffectiveDurationMin { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsConfirmedRescheduleFlow { get; set; }
}