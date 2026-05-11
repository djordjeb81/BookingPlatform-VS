namespace BookingPlatform.Contracts.Staff;

public sealed class StaffResourceSelectionItemDto
{
    public long ResourceId { get; set; }
    public string ResourceName { get; set; } = string.Empty;
    public int ResourceType { get; set; }
    public bool IsAssigned { get; set; }
}