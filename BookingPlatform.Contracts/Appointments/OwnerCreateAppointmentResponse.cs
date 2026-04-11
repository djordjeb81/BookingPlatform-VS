namespace BookingPlatform.Contracts.Appointments;

public sealed class OwnerCreateAppointmentResponse
{
    public long Id { get; set; }
    public long BusinessId { get; set; }
    public long ServiceId { get; set; }
    public long? PrimaryStaffMemberId { get; set; }

    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;
    public DateTime StartAtUtc { get; set; }
    public DateTime EndAtUtc { get; set; }

    public bool IgnoreAvailabilityRules { get; set; }
    public bool LegacyMasterOverride { get; set; }

    public bool EffectiveIgnoreWorkingHours { get; set; }
    public bool EffectiveIgnoreTimeOffBlocks { get; set; }
    public bool EffectiveIgnoreAppointmentConflicts { get; set; }

    public bool HasBusinessHoursViolation { get; set; }
    public bool HasStaffHoursViolation { get; set; }
    public bool HasTimeOffConflict { get; set; }
    public bool HasAppointmentConflict { get; set; }
    public bool BypassedWorkingHours { get; set; }
    public bool BypassedTimeOffBlocks { get; set; }
    public bool BypassedAppointmentConflicts { get; set; }

    public bool WasAvailableByRules { get; set; }
    public string CreationMode { get; set; } = string.Empty;
    public string CreationModeLabel { get; set; } = string.Empty;
    public List<string> AppliedOverrides { get; set; } = new();
    public List<string> AppliedOverrideLabels { get; set; } = new();
    public string Message { get; set; } = string.Empty;
    public List<string> ReasonCodes { get; set; } = new();
}