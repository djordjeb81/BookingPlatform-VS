namespace SmartBooking_Desk.Models.Resources
{
    public class ResourceItemDto
    {
        public long Id { get; set; }
        public long BusinessId { get; set; }
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public bool CreatesOccupancy { get; set; }
    }
}