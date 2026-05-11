using System;
using System.Security.Cryptography;
using System.Text;

namespace SmartBooking_Desk.Services.Licensing
{
    public class HwidService
    {
        public string GetComputerName()
        {
            return Environment.MachineName ?? "Nepoznat računar";
        }

        public string GetProgramVersion()
        {
            return "1.0.0";
        }

        public string GetHwidHash()
        {
            var raw = $"{Environment.MachineName}|{Environment.UserName}|SmartBookingDesk";
            var bytes = Encoding.UTF8.GetBytes(raw);
            var hash = SHA256.HashData(bytes);
            return Convert.ToHexString(hash);
        }
    }
}