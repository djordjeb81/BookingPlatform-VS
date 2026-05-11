using BookingPlatform.Domain.Common;
using BookingPlatform.Domain.Staff;

namespace BookingPlatform.Domain.Services;

public sealed class ServiceResourceUsageStaffMember : AuditableEntity
{
    public long ServiceResourceUsageId { get; set; }

    public ServiceResourceUsage ServiceResourceUsage { get; set; } = null!;

    public long StaffMemberId { get; set; }

    public StaffMember StaffMember { get; set; } = null!;
}