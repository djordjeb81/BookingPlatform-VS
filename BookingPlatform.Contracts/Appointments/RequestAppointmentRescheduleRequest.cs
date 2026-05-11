namespace BookingPlatform.Contracts.Appointments;

public sealed class RequestAppointmentRescheduleRequest
{
    public long AppointmentId { get; set; }
    public DateTime ProposedStartAtUtc { get; set; }
    public string? Message { get; set; }
}