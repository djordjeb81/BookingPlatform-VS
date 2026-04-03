namespace BookingPlatform.Contracts.Scheduling;

public sealed class SearchAvailableSlotsRequest
{
    public long BusinessId { get; set; }
    public long ServiceId { get; set; }
    public long? StaffMemberId { get; set; }
    public DateTime Date { get; set; }
}