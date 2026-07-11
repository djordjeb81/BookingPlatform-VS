namespace BookingPlatform.Contracts.Fitness;

public sealed class CustomerCreateFitnessBookingResponse
{
    public bool Success { get; set; }

    public long? BookingId { get; set; }

    public int? BookingStatus { get; set; }

    public string? BookingStatusText { get; set; }

    public string Message { get; set; } = string.Empty;

    public bool MembershipWasActive { get; set; }

    public string? MembershipWarningText { get; set; }
}