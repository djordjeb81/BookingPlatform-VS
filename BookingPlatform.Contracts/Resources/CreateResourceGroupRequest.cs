namespace BookingPlatform.Contracts.Resources;

public sealed class CreateResourceGroupRequest
{
    public long BusinessId { get; set; }

    public string Name { get; set; } = string.Empty;
}