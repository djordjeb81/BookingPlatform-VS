namespace SmartBooking_Desk.Models.Appointments
{
    public class AcceptRescheduleRequestDto
    {
        public long AppointmentId { get; set; }
        public long ChangeRequestId { get; set; }
    }
}