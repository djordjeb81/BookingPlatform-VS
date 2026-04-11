namespace BookingPlatform.Contracts.Appointments;

public sealed class CreateOwnerAppointmentRequest
{
    public long BusinessId { get; set; }
    public long ServiceId { get; set; }
    public long? PrimaryStaffMemberId { get; set; }

    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;

    public DateTime StartAtUtc { get; set; }

    public int? FinalDurationMin { get; set; }

    public string? Notes { get; set; }

    // Privremeno ostaje kao master override zbog kompatibilnosti.
    // Legacy master override zbog kompatibilnosti sa starijim frontend-om.
    public bool IgnoreAvailabilityRules { get; set; }

    // Novi finiji bypass flagovi.
    public bool IgnoreWorkingHours { get; set; }
    public bool IgnoreTimeOffBlocks { get; set; }
    public bool IgnoreAppointmentConflicts { get; set; }
}