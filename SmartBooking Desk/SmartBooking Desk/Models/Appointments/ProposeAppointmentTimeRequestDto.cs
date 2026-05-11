using System;

namespace SmartBooking_Desk.Models.Appointments
{
    public class ProposeAppointmentTimeRequestDto
    {
        public long AppointmentId { get; set; }
        public DateTime ProposedStartAtUtc { get; set; }
        public int? FinalDurationMin { get; set; }
        public string? Message { get; set; }
    }
}