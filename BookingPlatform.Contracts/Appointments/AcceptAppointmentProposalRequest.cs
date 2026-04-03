namespace BookingPlatform.Contracts.Appointments;

public sealed class AcceptAppointmentProposalRequest
{
    public long AppointmentId { get; set; }
    public long ChangeRequestId { get; set; }
}