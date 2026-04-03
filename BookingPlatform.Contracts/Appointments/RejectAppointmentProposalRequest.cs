namespace BookingPlatform.Contracts.Appointments;

public sealed class RejectAppointmentProposalRequest
{
    public long AppointmentId { get; set; }
    public long ChangeRequestId { get; set; }
    public string? Reason { get; set; }
}