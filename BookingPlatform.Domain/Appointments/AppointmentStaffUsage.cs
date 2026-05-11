using BookingPlatform.Domain.Common;
using BookingPlatform.Domain.Staff;

namespace BookingPlatform.Domain.Appointments;

public sealed class AppointmentStaffUsage : AuditableEntity
{
    public long AppointmentId { get; set; }

    public Appointment Appointment { get; set; } = null!;

    public long StaffMemberId { get; set; }

    public StaffMember StaffMember { get; set; } = null!;

    public int StartMinute { get; set; }

    public int DurationMin { get; set; }
}