namespace SmartBooking_Desk.Models.Staff
{
    public class StaffResourceAssignmentDto
    {
        public long ResourceId { get; set; }
        public string ResourceName { get; set; } = "";
        public int ResourceType { get; set; }
        public bool IsAssigned { get; set; }
    }
}