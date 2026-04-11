namespace BookingPlatform.Contracts.Appointments;

public sealed class AppointmentChangeRequestItemResponse
{
    public long Id { get; set; }
    public long AppointmentId { get; set; }
    public string RequestType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string InitiatedBy { get; set; } = string.Empty;
    public DateTime OriginalStartAtUtc { get; set; }
    public DateTime OriginalEndAtUtc { get; set; }
    public DateTime ProposedStartAtUtc { get; set; }
    public DateTime ProposedEndAtUtc { get; set; }
    public string? Reason { get; set; }
    public string? Message { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public DateTime? RespondedAtUtc { get; set; }
}