namespace BookingPlatform.Contracts.Appointments;

public sealed class AppointmentDelayLimitResponse
{
    public long AppointmentId { get; set; }

    public int MaxDelayMinutes { get; set; }

    public List<int> AllowedDelayMinutes { get; set; } = new();

    public string Message { get; set; } = "";
}