using BookingPlatform.Domain.Common;

namespace BookingPlatform.Domain.Services;

public sealed class ServiceResourceUsage : AuditableEntity
{
    public long ServiceId { get; set; }

    public long ResourceId { get; set; }

    // Legacy polje. Ostavljamo ga privremeno da ne lomimo stare podatke i Desk odmah.
    // Nova logika ide preko StaffMembers kolekcije ispod.
    public long? StaffId { get; set; }

    public int StartMinute { get; set; }

    public int DurationMin { get; set; }

    public bool IsRequired { get; set; } = true;
    public string? CustomerDisplayText { get; set; }

    public ICollection<ServiceResourceUsageStaffMember> StaffMembers { get; set; }
        = new List<ServiceResourceUsageStaffMember>();
}