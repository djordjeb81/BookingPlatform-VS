namespace SmartBooking_Desk.Models.Services
{
    public class ServiceStepItemDto
    {
        public long Id { get; set; }
        public long ServiceId { get; set; }
        public int StepOrder { get; set; }
        public string Name { get; set; } = "";
        public int DurationMin { get; set; }
        public bool ClientPresenceRequired { get; set; }
        public bool SameStaffAsPrevious { get; set; }
    }
}