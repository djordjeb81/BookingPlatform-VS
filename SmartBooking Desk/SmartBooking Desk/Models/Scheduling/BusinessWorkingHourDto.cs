namespace SmartBooking_Desk.Models.Scheduling
{
    public class BusinessWorkingHourDto
    {
        public long Id { get; set; }
        public long BusinessId { get; set; }
        public int DayOfWeek { get; set; }
        public string StartTime { get; set; } = "";
        public string EndTime { get; set; } = "";
        public bool IsWorkingDay { get; set; }
    }
}