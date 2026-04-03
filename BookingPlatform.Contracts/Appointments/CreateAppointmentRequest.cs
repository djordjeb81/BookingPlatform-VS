namespace BookingPlatform.Contracts.Appointments;

public sealed class CreateAppointmentRequest
{
    public long BusinessId { get; set; }
    public long ServiceId { get; set; }
    public long? PrimaryStaffMemberId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public DateTime StartAtUtc { get; set; }
    public string? Notes { get; set; }
}