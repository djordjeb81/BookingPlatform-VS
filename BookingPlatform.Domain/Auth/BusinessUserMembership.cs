using BookingPlatform.Domain.Businesses;
using BookingPlatform.Domain.Common;

namespace BookingPlatform.Domain.Auth;

public sealed class BusinessUserMembership : AuditableEntity
{
    public long AppUserId { get; set; }
    public long BusinessId { get; set; }
    public BusinessUserRole Role { get; set; } = BusinessUserRole.Staff;
    public bool IsActive { get; set; } = true;

    public AppUser AppUser { get; set; } = null!;
    public Business Business { get; set; } = null!;
}