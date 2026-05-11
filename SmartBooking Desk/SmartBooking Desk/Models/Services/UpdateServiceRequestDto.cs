namespace SmartBooking_Desk.Models.Services
{
    public class UpdateServiceRequestDto
    {
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public double? BasePrice { get; set; }
        public int EstimatedDurationMin { get; set; }
        public int BookingStrategyType { get; set; }
        public bool IsActive { get; set; }
    }
}