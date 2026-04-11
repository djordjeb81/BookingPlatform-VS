namespace BookingPlatform.Contracts.Appointments;

public sealed class AppointmentChangeActionResponse
{
    public long AppointmentId { get; set; }
    public string AppointmentStatus { get; set; } = string.Empty;

    public DateTime? StartAtUtc { get; set; }
    public DateTime? EndAtUtc { get; set; }
    public int? DurationMin { get; set; }

    public long? ChangeRequestId { get; set; }
    public string? ChangeRequestStatus { get; set; }

    public string Message { get; set; } = string.Empty;
}