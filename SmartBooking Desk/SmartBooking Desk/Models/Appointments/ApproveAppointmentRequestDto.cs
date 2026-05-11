namespace SmartBooking_Desk.Models.Appointments
{
    public class ApproveAppointmentRequestDto
    {
        public long AppointmentId { get; set; }
        public int? FinalDurationMin { get; set; }
    }
}