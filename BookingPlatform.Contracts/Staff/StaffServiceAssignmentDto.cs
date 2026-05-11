namespace BookingPlatform.Contracts.Staff;

public sealed class StaffServiceAssignmentDto
{
    public long StaffMemberId { get; set; }
    public long ServiceId { get; set; }
}