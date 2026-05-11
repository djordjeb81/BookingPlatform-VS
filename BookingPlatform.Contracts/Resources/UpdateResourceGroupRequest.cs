namespace BookingPlatform.Contracts.Resources;

public sealed class UpdateResourceGroupRequest
{
    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}