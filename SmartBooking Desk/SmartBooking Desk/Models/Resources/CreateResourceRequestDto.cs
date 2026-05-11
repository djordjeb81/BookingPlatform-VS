namespace SmartBooking_Desk.Models.Resources
{
    public class CreateResourceRequestDto
    {
        public long BusinessId { get; set; }
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public bool CreatesOccupancy { get; set; }
    }
}