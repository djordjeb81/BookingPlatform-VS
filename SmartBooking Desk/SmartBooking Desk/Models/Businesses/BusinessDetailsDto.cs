namespace SmartBooking_Desk.Models.Businesses
{
    public class BusinessDetailsDto
    {
        public long Id { get; set; }
        public string Name { get; set; } = "";
        public int BusinessType { get; set; }
        public string? Description { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public int SlotIntervalMin { get; set; }

        public string? Street { get; set; }
        public string? StreetNumber { get; set; }
        public string? City { get; set; }
        public string? PostalCode { get; set; }
        public string? Country { get; set; }

        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? GooglePlaceId { get; set; }

        public bool IsActive { get; set; }
    }
}