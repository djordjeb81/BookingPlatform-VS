namespace BookingPlatform.Contracts.Staff;

public sealed class UpdateStaffMemberRequest
{
    public string DisplayName { get; set; } = string.Empty;
    public string? Title { get; set; }
    public bool IsBookable { get; set; }
    public bool IsActive { get; set; }
}