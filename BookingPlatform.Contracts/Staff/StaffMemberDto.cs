namespace BookingPlatform.Contracts.Staff;

public sealed class StaffMemberDto
{
    public long Id { get; set; }
    public long BusinessId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? Title { get; set; }
    public bool IsBookable { get; set; }
    public bool IsActive { get; set; }
}