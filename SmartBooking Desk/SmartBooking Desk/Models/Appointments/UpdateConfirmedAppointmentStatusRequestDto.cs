namespace SmartBooking_Desk.Models.Appointments
{
    public class UpdateConfirmedAppointmentStatusRequestDto
    {
        public long AppointmentId { get; set; }
        public string? Note { get; set; }
    }
}