namespace SmartBooking_Desk.Models.Scheduling
{
    public sealed class StaffScheduleRuleDto
    {
        public long Id { get; set; }
        public long StaffMemberId { get; set; }
        public int DayOfWeek { get; set; }
        public int WeekType { get; set; }
        public int SegmentType { get; set; }
        public string? StartTime { get; set; }
        public string? EndTime { get; set; }
        public bool IsActive { get; set; }
    }

    public sealed class StaffScheduleRuleRowDto
    {
        public int DayOfWeek { get; set; }
        public int WeekType { get; set; }
        public int SegmentType { get; set; }
        public string? StartTime { get; set; }
        public string? EndTime { get; set; }
        public bool IsActive { get; set; }
    }

    public sealed class ReplaceStaffScheduleRulesRequestDto
    {
        public long StaffMemberId { get; set; }
        public int ScheduleMode { get; set; }
        public List<StaffScheduleRuleRowDto> Rules { get; set; } = new();
    }
}