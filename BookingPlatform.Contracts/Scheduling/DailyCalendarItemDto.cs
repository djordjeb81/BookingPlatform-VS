namespace BookingPlatform.Contracts.Scheduling;

public sealed class DailyCalendarItemDto
{
    public string ItemType { get; set; } = string.Empty;
    // Appointment | Block

    public long? AppointmentId { get; set; }
    public long? BlockId { get; set; }

    public long BusinessId { get; set; }
    public long? StaffMemberId { get; set; }
    public string? StaffDisplayName { get; set; }

    public DateTime StartAtUtc { get; set; }
    public DateTime EndAtUtc { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }

    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }

    public string? AppointmentStatus { get; set; }
    public string? BlockType { get; set; }

    public string StartLabel { get; set; } = string.Empty;
    public string EndLabel { get; set; } = string.Empty;
}