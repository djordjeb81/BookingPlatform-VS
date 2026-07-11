using BookingPlatform.Domain.Businesses;
using BookingPlatform.Domain.Common;
using BookingPlatform.Domain.Staff;

namespace BookingPlatform.Domain.Fitness;

public sealed class FitnessSessionTemplate : AuditableEntity
{
    public long BusinessId { get; set; }

    public Business Business { get; set; } = null!;

    public long FitnessRoomId { get; set; }

    public FitnessRoom FitnessRoom { get; set; } = null!;

    public long? FitnessClassTypeId { get; set; }

    public FitnessClassType? FitnessClassType { get; set; }

    public long? TrainerStaffMemberId { get; set; }

    public StaffMember? TrainerStaffMember { get; set; }

    public FitnessSessionType SessionType { get; set; } = FitnessSessionType.Group;

    /// <summary>
    /// 1 = Ponedeljak, 2 = Utorak, ..., 7 = Nedelja.
    /// </summary>
    public int DayOfWeek { get; set; }

    public TimeOnly StartTime { get; set; }

    public int DurationMin { get; set; } = 60;

    public int Capacity { get; set; }

    public bool IsActive { get; set; } = true;

    public DateOnly? ValidFromDate { get; set; }

    public DateOnly? ValidToDate { get; set; }

    public string? Note { get; set; }

    public List<FitnessSession> Sessions { get; set; } = new();
}