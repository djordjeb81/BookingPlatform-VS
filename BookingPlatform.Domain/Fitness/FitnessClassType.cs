using BookingPlatform.Domain.Businesses;
using BookingPlatform.Domain.Common;

namespace BookingPlatform.Domain.Fitness;

public sealed class FitnessClassType : AuditableEntity
{
    public long BusinessId { get; set; }

    public Business Business { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int DefaultDurationMin { get; set; } = 60;

    public int? DefaultCapacity { get; set; }

    public bool IsActive { get; set; } = true;

    public int DisplayOrder { get; set; }

    public List<FitnessSession> Sessions { get; set; } = new();

    public List<FitnessSessionTemplate> SessionTemplates { get; set; } = new();
}