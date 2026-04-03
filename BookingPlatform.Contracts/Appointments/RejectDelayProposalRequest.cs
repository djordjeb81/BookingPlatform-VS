namespace BookingPlatform.Contracts.Appointments;

public sealed class RejectDelayProposalRequest
{
    public long AppointmentId { get; set; }
    public long ChangeRequestId { get; set; }
    public string? Reason { get; set; }
}