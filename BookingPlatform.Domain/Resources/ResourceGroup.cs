using BookingPlatform.Domain.Common;
using BookingPlatform.Domain.Businesses;

namespace BookingPlatform.Domain.Resources;

public sealed class ResourceGroup : AuditableEntity
{
    public long BusinessId { get; set; }

    public Business Business { get; set; } = null!;

    public string Name { get; set; } = "";

    public bool IsActive { get; set; } = true;

    public ICollection<Resource> Resources { get; set; } = new List<Resource>();
}