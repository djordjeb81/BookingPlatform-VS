namespace BookingPlatform.Contracts.Appointments;

public sealed class CreateAppointmentResponse
{
    public long Id { get; set; }
    public long BusinessId { get; set; }
    public long ServiceId { get; set; }
    public long? PrimaryStaffMemberId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime StartAtUtc { get; set; }
    public DateTime EndAtUtc { get; set; }
    public long ChangeRequestId { get; set; }
    public string ChangeRequestStatus { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}