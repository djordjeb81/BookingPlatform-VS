using BookingPlatform.Domain.Common;

namespace BookingPlatform.Domain.Staff;

public sealed class StaffServiceAssignment : Entity
{
    public long StaffMemberId { get; set; }
    public long ServiceId { get; set; }
}