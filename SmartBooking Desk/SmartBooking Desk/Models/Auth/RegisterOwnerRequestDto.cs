namespace SmartBooking_Desk.Models.Auth
{
    public class RegisterOwnerRequestDto
    {
        public string FullName { get; set; } = "";
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";

        public string BusinessName { get; set; } = "";
        public int BusinessType { get; set; }

        public string? Description { get; set; }
        public string? Phone { get; set; }
        public string? BusinessEmail { get; set; }

        public int SlotIntervalMin { get; set; } = 30;

        public string? Street { get; set; }
        public string? StreetNumber { get; set; }
        public string? City { get; set; }
        public string? PostalCode { get; set; }
        public string? Country { get; set; }

        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? GooglePlaceId { get; set; }
    }
}