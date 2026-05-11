using System;

namespace SmartBooking_Desk.Models.Appointments
{
    public class AppointmentActionResponseDto
    {
        public long AppointmentId { get; set; }
        public string? AppointmentStatus { get; set; }
        public string? Action { get; set; }
        public DateTime StartAtUtc { get; set; }
        public DateTime EndAtUtc { get; set; }
        public DateTime? ScheduledAtUtc { get; set; }
        public string? Message { get; set; }
    }
}