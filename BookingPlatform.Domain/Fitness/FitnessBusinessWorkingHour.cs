using BookingPlatform.Domain.Businesses;
using BookingPlatform.Domain.Common;

namespace BookingPlatform.Domain.Fitness;

public sealed class FitnessBusinessWorkingHour : AuditableEntity
{
    public long BusinessId { get; set; }

    public Business Business { get; set; } = null!;

    /// <summary>
    /// 1 = Ponedeljak, 2 = Utorak, ..., 7 = Nedelja.
    /// </summary>
    public int DayOfWeek { get; set; }

    public bool IsClosed { get; set; }

    public TimeOnly? OpenTime { get; set; }

    public TimeOnly? CloseTime { get; set; }
}