namespace BookingPlatform.Contracts.Appointments;

public sealed class ProposeDelayRequest
{
    public long AppointmentId { get; set; }
    public int DelayMinutes { get; set; }
    public string? Message { get; set; }
}