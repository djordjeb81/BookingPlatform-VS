using System.Collections.Generic;

namespace SmartBooking_Desk.Models.Scheduling
{
    public class UpdateBusinessWorkingHoursRequestDto
    {
        public long BusinessId { get; set; }
        public List<BusinessWorkingHourRowDto> WorkingHours { get; set; } = new();
    }

    public class BusinessWorkingHourRowDto
    {
        public int DayOfWeek { get; set; }
        public string StartTime { get; set; } = "";
        public string EndTime { get; set; } = "";
        public bool IsWorkingDay { get; set; }
    }
}