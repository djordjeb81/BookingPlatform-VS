namespace BookingPlatform.Contracts.Staff;

public sealed class CreateStaffMemberRequest
{
    public long BusinessId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? Title { get; set; }
    public bool IsBookable { get; set; } = true;
}