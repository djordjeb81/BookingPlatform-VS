using System.Collections.Generic;

namespace SmartBooking_Desk.Models.Staff
{
    public class UpdateStaffResourcesRequestDto
    {
        public List<long> ResourceIds { get; set; } = new();
    }
}