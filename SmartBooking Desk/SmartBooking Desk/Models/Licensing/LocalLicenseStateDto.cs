using System;

namespace SmartBooking_Desk.Models.Licensing
{
    public class LocalLicenseStateDto
    {
        public string Email { get; set; } = "";
        public string HwidHash { get; set; } = "";
        public string ComputerName { get; set; } = "";
        public string ProgramVersion { get; set; } = "";

        public bool IsRegistered { get; set; }
        public bool IsApproved { get; set; }

        public string Status { get; set; } = "";
        public string LicenseToken { get; set; } = "";

        public DateTime? LastSuccessfulLicenseCheckUtc { get; set; }
        public DateTime? LastLicenseRefreshAtUtc { get; set; }
        public DateTime? ValidUntilUtc { get; set; }

        public DateTime? LastOnlineAttemptUtc { get; set; }

        public bool CanWorkOffline()
        {
            return ValidUntilUtc.HasValue && DateTime.UtcNow <= ValidUntilUtc.Value;
        }

        public bool ShouldTryOnlineRefresh()
        {
            if (!LastSuccessfulLicenseCheckUtc.HasValue)
                return true;

            return DateTime.UtcNow >= LastSuccessfulLicenseCheckUtc.Value.AddHours(24);
        }
    }
}