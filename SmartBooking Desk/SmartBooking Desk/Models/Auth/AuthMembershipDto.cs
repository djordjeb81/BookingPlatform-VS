namespace SmartBooking_Desk.Models.Auth
{
    public class AuthMembershipDto
    {
        public long BusinessId { get; set; }
        public string BusinessName { get; set; } = "";
        public string Role { get; set; } = "";
        public bool IsActive { get; set; }
    }
}