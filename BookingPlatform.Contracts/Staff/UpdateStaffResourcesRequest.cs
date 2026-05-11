namespace BookingPlatform.Contracts.Staff;

public sealed class UpdateStaffResourcesRequest
{
    public List<long> ResourceIds { get; set; } = new();
}