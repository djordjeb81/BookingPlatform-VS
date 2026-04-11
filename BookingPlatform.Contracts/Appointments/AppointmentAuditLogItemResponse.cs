namespace BookingPlatform.Contracts.Appointments;

public sealed class AppointmentAuditLogItemResponse
{
    public long Id { get; set; }
    public long AppointmentId { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string? Message { get; set; }
    public string? OldValuesJson { get; set; }
    public string? NewValuesJson { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}