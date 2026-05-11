namespace SmartBooking_Desk.Models.Licensing
{
    public class RegisterDeviceRequestDto
    {
        public string HwidHash { get; set; } = "";
        public string ComputerName { get; set; } = "";
        public string ProgramVersion { get; set; } = "";
    }
}