namespace BookingPlatform.Contracts.BusinessPortal;

public sealed class BusinessPortalServiceDto
{
    public long Id { get; set; }

    public long BusinessId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int? EstimatedDurationMin { get; set; }

    public bool IsActive { get; set; }
}