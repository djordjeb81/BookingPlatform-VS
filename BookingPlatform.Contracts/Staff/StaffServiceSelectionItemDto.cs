namespace BookingPlatform.Contracts.Staff;

public sealed class StaffServiceSelectionItemDto
{
    public long ServiceId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public bool IsAssigned { get; set; }
}