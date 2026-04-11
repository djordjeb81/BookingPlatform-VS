namespace BookingPlatform.Contracts.Appointments;

public sealed class MarkAppointmentCallActionRequest
{
    public long AppointmentId { get; set; }

    // Ako je poslato, owner zadaje stvarno trajanje termina u minutima.
    public int? FinalDurationMin { get; set; }

    public string? Note { get; set; }
}