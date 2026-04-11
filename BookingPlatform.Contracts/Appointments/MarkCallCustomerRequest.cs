namespace BookingPlatform.Contracts.Appointments;

public sealed class MarkCallCustomerRequest
{
    public long AppointmentId { get; set; }
    public string? Note { get; set; }
}