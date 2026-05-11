namespace SmartBooking_Desk.Models.Scheduling
{
    public sealed class StaffScheduleOverrideDto
    {
        public long Id { get; set; }
        public long StaffMemberId { get; set; }
        public DateTime Date { get; set; }
        public int OverrideType { get; set; }
        public int? ShiftType { get; set; }
        public string? StartTime { get; set; }
        public string? EndTime { get; set; }
        public bool IsDayOff { get; set; }
        public string? Reason { get; set; }
    }

    public sealed class CreateOrUpdateStaffScheduleOverrideRequestDto
    {
        public long StaffMemberId { get; set; }
        public DateTime Date { get; set; }
        public int OverrideType { get; set; }
        public int? ShiftType { get; set; }
        public string? StartTime { get; set; }
        public string? EndTime { get; set; }
        public bool IsDayOff { get; set; }
        public string? Reason { get; set; }
    }
}