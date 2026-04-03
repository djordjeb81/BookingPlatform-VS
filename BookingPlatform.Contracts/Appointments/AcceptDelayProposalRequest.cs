namespace BookingPlatform.Contracts.Appointments;

public sealed class AcceptDelayProposalRequest
{
    public long AppointmentId { get; set; }
    public long ChangeRequestId { get; set; }
}