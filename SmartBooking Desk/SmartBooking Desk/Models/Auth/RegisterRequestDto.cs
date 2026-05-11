namespace SmartBooking_Desk.Models.Auth
{
    public class RegisterRequestDto
    {
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
        public string FullName { get; set; } = "";
        public long? InitialBusinessId { get; set; }
    }
}