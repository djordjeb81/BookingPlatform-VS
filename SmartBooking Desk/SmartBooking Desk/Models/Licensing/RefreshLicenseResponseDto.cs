using System;

namespace SmartBooking_Desk.Models.Licensing
{
    public class RefreshLicenseResponseDto
    {
        public long DeviceId { get; set; }
        public string Status { get; set; } = "";
        public bool IsApproved { get; set; }
        public string? LicenseToken { get; set; }
        public DateTime? ValidUntilUtc { get; set; }
        public DateTime? LastSeenAtUtc { get; set; }
        public DateTime? LastLicenseRefreshAtUtc { get; set; }
        public string Message { get; set; } = "";
    }
}