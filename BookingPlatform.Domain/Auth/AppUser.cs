using BookingPlatform.Domain.Common;

namespace BookingPlatform.Domain.Auth;

public sealed class AppUser : AuditableEntity
{
    public string Email { get; set; } = string.Empty;
    public string NormalizedEmail { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public ICollection<BusinessUserMembership> BusinessMemberships { get; set; } =
        new List<BusinessUserMembership>();
}