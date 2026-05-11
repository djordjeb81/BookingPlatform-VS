namespace SmartBooking_Desk.Models.Resources
{
    public class UpdateResourceRequestDto
    {
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public bool CreatesOccupancy { get; set; }
    }
}