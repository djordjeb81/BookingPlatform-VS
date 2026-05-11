namespace SmartBooking_Desk.Models.Staff
{
    public class StaffItemDto
    {
        public long Id { get; set; }
        public long BusinessId { get; set; }
        public string DisplayName { get; set; } = "";
        public string? Title { get; set; }
        public bool IsBookable { get; set; }
        public int ScheduleMode { get; set; }
        public bool IsActive { get; set; }
    }
}