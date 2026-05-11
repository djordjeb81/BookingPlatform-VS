namespace SmartBooking_Desk.Models.Staff
{
    public class StaffServiceAssignmentDto
    {
        public long ServiceId { get; set; }
        public string ServiceName { get; set; } = "";
        public bool IsAssigned { get; set; }
    }
}