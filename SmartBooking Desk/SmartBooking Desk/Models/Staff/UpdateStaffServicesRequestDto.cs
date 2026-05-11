using System.Collections.Generic;

namespace SmartBooking_Desk.Models.Staff
{
    public class UpdateStaffServicesRequestDto
    {
        public List<long> ServiceIds { get; set; } = new();
    }
}