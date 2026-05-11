using System;

namespace SmartBooking_Desk.Models.Appointments
{
    public class AppointmentChangeActionResponseDto
    {
        public long AppointmentId { get; set; }
        public string? AppointmentStatus { get; set; }
        public long ChangeRequestId { get; set; }
        public string? ChangeRequestStatus { get; set; }
        public DateTime StartAtUtc { get; set; }
        public DateTime EndAtUtc { get; set; }
        public int DurationMin { get; set; }
        public string? Message { get; set; }
    }
}