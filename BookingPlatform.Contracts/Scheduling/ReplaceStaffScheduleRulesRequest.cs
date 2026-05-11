namespace BookingPlatform.Contracts.Scheduling;

public sealed class ReplaceStaffScheduleRulesRequest
{
    public long StaffMemberId { get; set; }
    public int ScheduleMode { get; set; }
    public List<StaffScheduleRuleRowDto> Rules { get; set; } = new();
}