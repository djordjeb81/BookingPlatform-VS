namespace BookingPlatform.Contracts.Appointments;

public sealed class ProposeAppointmentTimeRequest
{
    public long AppointmentId { get; set; }
    public DateTime ProposedStartAtUtc { get; set; }
    public int? FinalDurationMin { get; set; }
    public string? Message { get; set; }
}