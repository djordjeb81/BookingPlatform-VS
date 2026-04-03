using BookingPlatform.Domain.Common;

namespace BookingPlatform.Domain.Staff;

public sealed class StaffMember : AuditableEntity
{
    public long BusinessId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? Title { get; set; }
    public bool IsBookable { get; set; } = true;
    public bool IsActive { get; set; } = true;
}