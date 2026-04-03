namespace BookingPlatform.Contracts.Scheduling;

public sealed class FirstAvailableSearchRequest
{
    public long BusinessId { get; set; }
    public long ServiceId { get; set; }
    public long? StaffMemberId { get; set; }
    public DateTime? StartDate { get; set; }
    public int SearchDays { get; set; } = 30;
}