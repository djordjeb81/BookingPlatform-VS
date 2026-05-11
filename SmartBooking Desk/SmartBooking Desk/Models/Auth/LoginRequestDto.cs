namespace SmartBooking_Desk.Models.Auth
{
    public class LoginRequestDto
    {
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
    }
}