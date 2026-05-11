namespace SmartBooking_Desk.Models.Resources
{
    public class UpdateServiceResourceUsageRequestDto
    {
        public long ServiceId { get; set; }
        public long? StaffId { get; set; }
        public long ResourceId { get; set; }
        public int StartMinute { get; set; }
        public int DurationMin { get; set; }
        public bool IsRequired { get; set; }
    }
}