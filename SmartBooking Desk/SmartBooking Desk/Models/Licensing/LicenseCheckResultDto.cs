namespace SmartBooking_Desk.Models.Licensing
{
    public class LicenseCheckResultDto
    {
        public bool IsAllowed { get; set; }
        public bool IsPendingApproval { get; set; }
        public bool IsBlocked { get; set; }
        public bool IsOfflineMode { get; set; }

        public string Message { get; set; } = "";
    }
}