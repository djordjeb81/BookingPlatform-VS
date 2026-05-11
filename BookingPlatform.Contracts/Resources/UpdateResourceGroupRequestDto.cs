namespace BookingPlatform.Contracts.Resources;

public sealed class UpdateResourceGroupRequestDto
{
    public string Name { get; set; } = "";

    public bool IsActive { get; set; } = true;
}