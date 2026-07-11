using BookingPlatform.Domain.Businesses;
using BookingPlatform.Domain.Common;

namespace BookingPlatform.Domain.Fitness;

public sealed class FitnessRoom : AuditableEntity
{
    public long BusinessId { get; set; }

    public Business Business { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public int Capacity { get; set; }

    public bool IsActive { get; set; } = true;

    public bool AllowsGroupClasses { get; set; } = true;

    public bool AllowsIndividualTraining { get; set; }

    public int DisplayOrder { get; set; }

    public List<FitnessSession> Sessions { get; set; } = new();

    public List<FitnessRoomWorkingHour> WorkingHours { get; set; } = new();

    public List<FitnessSessionTemplate> SessionTemplates { get; set; } = new();
}