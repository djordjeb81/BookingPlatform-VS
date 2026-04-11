namespace BookingPlatform.Contracts.Appointments;

public sealed class OwnerCreateAvailabilityErrorResponse
{
    public string Message { get; set; } = string.Empty;
    public string ReasonCode { get; set; } = string.Empty;
    public List<string> ReasonCodes { get; set; } = new();

    public bool HasSlotGridViolation { get; set; }
    public bool HasBusinessHoursViolation { get; set; }
    public bool HasStaffHoursViolation { get; set; }
    public bool HasTimeOffConflict { get; set; }
    public bool HasAppointmentConflict { get; set; }

    public bool BypassedSlotGrid { get; set; }
    public bool BypassedWorkingHours { get; set; }
    public bool BypassedTimeOffBlocks { get; set; }
    public bool BypassedAppointmentConflicts { get; set; }

    public bool EffectiveIgnoreWorkingHours { get; set; }
    public bool EffectiveIgnoreTimeOffBlocks { get; set; }
    public bool EffectiveIgnoreAppointmentConflicts { get; set; }
    public bool LegacyMasterOverride { get; set; }

    public List<string> AppliedOverrides { get; set; } = new();
    public List<string> AppliedOverrideLabels { get; set; } = new();
}