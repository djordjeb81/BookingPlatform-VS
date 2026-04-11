namespace BookingPlatform.Contracts.Appointments;

public sealed class ApproveAppointmentRequest
{
    public long AppointmentId { get; set; }

    // Ako nije poslato, koristi se postojeće trajanje termina.
    // Ako jeste poslato, owner određuje stvarno trajanje termina u minutima.
    public int? FinalDurationMin { get; set; }
}