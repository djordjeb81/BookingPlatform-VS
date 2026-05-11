namespace BookingPlatform.Contracts.CustomerPortal;

public sealed class CustomerPortalAppointmentDto
{
    public long Id { get; set; }

    public long BusinessId { get; set; }

    public string BusinessName { get; set; } = string.Empty;

    public long BusinessCustomerId { get; set; }

    public long ServiceId { get; set; }

    public string? ServiceName { get; set; }

    public long? PrimaryStaffMemberId { get; set; }

    public string? StaffDisplayName { get; set; }

    public long? ResourceId { get; set; }

    public DateTime StartAtUtc { get; set; }

    public DateTime EndAtUtc { get; set; }

    public string Status { get; set; } = string.Empty;

    public string? Notes { get; set; }
}