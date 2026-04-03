using BookingPlatform.Domain.Common;

namespace BookingPlatform.Domain.Businesses;

public sealed class Business : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public BusinessType BusinessType { get; set; }
    public string? Description { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public bool IsActive { get; set; } = true;
}