using BookingPlatform.Domain.Common;

namespace BookingPlatform.Domain.Staff;

public sealed class StaffResourceAssignment : Entity
{
    public long StaffMemberId { get; set; }
    public long ResourceId { get; set; }
}