using BookingPlatform.Domain.Common;

namespace BookingPlatform.Domain.Resources;

public sealed class Resource : AuditableEntity
{
    public long BusinessId { get; set; }
    public string Name { get; set; } = string.Empty;
    public ResourceType ResourceType { get; set; }
    public int? Capacity { get; set; }
    public bool IsActive { get; set; } = true;
}