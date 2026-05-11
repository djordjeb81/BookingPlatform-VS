namespace BookingPlatform.Contracts.Appointments;

public sealed class CreateAppointmentErrorResponse
{
    public string Message { get; set; } = string.Empty;
    public string ReasonCode { get; set; } = string.Empty;
    public List<string> ReasonCodes { get; set; } = new();

    public bool HasSlotGridViolation { get; set; }
    public bool HasBusinessHoursViolation { get; set; }
    public bool HasStaffHoursViolation { get; set; }
    public bool HasTimeOffConflict { get; set; }
    public bool HasAppointmentConflict { get; set; }
    public bool HasResourceConflict { get; set; }
}