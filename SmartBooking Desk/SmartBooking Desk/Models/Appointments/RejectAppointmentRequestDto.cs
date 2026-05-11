namespace SmartBooking_Desk.Models.Appointments
{
    public class RejectAppointmentRequestDto
    {
        public long AppointmentId { get; set; }
        public string? Reason { get; set; }
    }
}