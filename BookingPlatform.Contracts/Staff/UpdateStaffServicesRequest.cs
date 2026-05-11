namespace BookingPlatform.Contracts.Staff;

public sealed class UpdateStaffServicesRequest
{
    public List<long> ServiceIds { get; set; } = new();
}