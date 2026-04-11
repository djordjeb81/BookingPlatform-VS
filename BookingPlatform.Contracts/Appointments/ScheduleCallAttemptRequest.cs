namespace BookingPlatform.Contracts.Appointments;

public sealed class ScheduleCallAttemptRequest
{
    public long AppointmentId { get; set; }
    public DateTime ScheduledAtUtc { get; set; }
    public string? Note { get; set; }
}