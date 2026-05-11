namespace SmartBooking_Desk.Models.Appointments
{
    public class ProposeDelayRequestDto
    {
        public long AppointmentId { get; set; }
        public int DelayMinutes { get; set; }
        public string? Message { get; set; }
    }
}