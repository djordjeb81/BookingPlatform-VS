namespace SmartBooking_Desk.Models.Staff
{
    public class CreateStaffRequestDto
    {
        public long BusinessId { get; set; }
        public string DisplayName { get; set; } = "";
        public string? Title { get; set; }
        public int ScheduleMode { get; set; }
        public bool IsBookable { get; set; }
    }
}
