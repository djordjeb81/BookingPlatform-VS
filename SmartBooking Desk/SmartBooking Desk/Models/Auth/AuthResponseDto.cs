using System;
using System.Collections.Generic;

namespace SmartBooking_Desk.Models.Auth
{
    public class AuthResponseDto
    {
        public long UserId { get; set; }
        public string Email { get; set; } = "";
        public string FullName { get; set; } = "";
        public string Token { get; set; } = "";
        public DateTime ExpiresAtUtc { get; set; }
        public List<AuthMembershipDto> Memberships { get; set; } = new();
    }
}