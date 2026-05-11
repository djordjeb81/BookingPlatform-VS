namespace BookingPlatform.Contracts.Resources;

public sealed class ResourceGroupDto
{
    public long Id { get; set; }

    public long BusinessId { get; set; }

    public string Name { get; set; } = "";

    public bool IsActive { get; set; }
}