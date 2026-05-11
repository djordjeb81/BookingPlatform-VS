namespace SmartBooking_Desk.Models.Appointments
{
    public class RejectRescheduleRequestDto
    {
        public long AppointmentId { get; set; }
        public long ChangeRequestId { get; set; }
        public string? Reason { get; set; }
    }
}