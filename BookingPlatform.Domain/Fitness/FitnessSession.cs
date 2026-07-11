using BookingPlatform.Domain.Businesses;
using BookingPlatform.Domain.Common;
using BookingPlatform.Domain.Staff;

namespace BookingPlatform.Domain.Fitness;

public sealed class FitnessSession : AuditableEntity
{
    public long BusinessId { get; set; }

    public Business Business { get; set; } = null!;

    public long FitnessRoomId { get; set; }

    public FitnessRoom FitnessRoom { get; set; } = null!;

    public long? FitnessClassTypeId { get; set; }

    public long? FitnessSessionTemplateId { get; set; }

    public FitnessSessionTemplate? FitnessSessionTemplate { get; set; }

    public FitnessClassType? FitnessClassType { get; set; }

    public long? TrainerStaffMemberId { get; set; }

    public StaffMember? TrainerStaffMember { get; set; }

    public FitnessSessionType SessionType { get; set; } = FitnessSessionType.Group;

    public DateTime StartAtUtc { get; set; }

    public DateTime EndAtUtc { get; set; }

    public int Capacity { get; set; }

    public FitnessSessionStatus Status { get; set; } = FitnessSessionStatus.Scheduled;

    public string? Note { get; set; }

    public List<FitnessSessionBooking> Bookings { get; set; } = new();
}