namespace BookingPlatform.Contracts.Scheduling;

public sealed class CreateTimeOffBlockRequest
{
    public long BusinessId { get; set; }
    public long? StaffMemberId { get; set; }

    public DateTime StartAtUtc { get; set; }
    public DateTime EndAtUtc { get; set; }

    public int BlockType { get; set; }
    public string? Reason { get; set; }
}