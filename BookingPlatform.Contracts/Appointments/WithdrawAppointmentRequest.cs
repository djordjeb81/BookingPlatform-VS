namespace BookingPlatform.Contracts.Appointments;

public sealed class WithdrawAppointmentRequest
{
    public long AppointmentId { get; set; }
    public string? Reason { get; set; }
}