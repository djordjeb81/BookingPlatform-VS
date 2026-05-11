namespace SmartBooking_Desk.Models.Resources
{
    public class ServiceResourceUsageDto
    {
        public long Id { get; set; }
        public long ServiceId { get; set; }
        public long? StaffId { get; set; }
        public long ResourceId { get; set; }
        public string ResourceName { get; set; } = "";
        public int ResourceType { get; set; }
        public int StartMinute { get; set; }
        public int DurationMin { get; set; }
        public bool IsRequired { get; set; }
    }
}