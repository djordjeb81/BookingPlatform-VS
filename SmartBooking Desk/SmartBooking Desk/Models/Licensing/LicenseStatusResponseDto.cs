using System;

namespace SmartBooking_Desk.Models.Licensing
{
    public class LicenseStatusResponseDto
    {
        public long DeviceId { get; set; }
        public string Status { get; set; } = "";
        public bool IsApproved { get; set; }
        public string HwidHash { get; set; } = "";
        public string ComputerName { get; set; } = "";
        public string ProgramVersion { get; set; } = "";
        public DateTime? LastSeenAtUtc { get; set; }
        public DateTime? LastLicenseRefreshAtUtc { get; set; }
        public DateTime? ValidUntilUtc { get; set; }
        public string Message { get; set; } = "";
    }
}