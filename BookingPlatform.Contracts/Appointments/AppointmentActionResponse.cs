namespace BookingPlatform.Contracts.Appointments;

public sealed class AppointmentActionResponse
{
    public long AppointmentId { get; set; }
    public string AppointmentStatus { get; set; } = string.Empty;
    public string? Action { get; set; }

    public DateTime? StartAtUtc { get; set; }
    public DateTime? EndAtUtc { get; set; }

    public DateTime? ScheduledAtUtc { get; set; }
    public string Message { get; set; } = string.Empty;
}