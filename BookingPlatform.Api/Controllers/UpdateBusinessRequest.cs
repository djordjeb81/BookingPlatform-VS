namespace BookingPlatform.Contracts.Businesses;

public sealed class UpdateBusinessRequest
{
    public string Name { get; set; } = string.Empty;
    public int BusinessType { get; set; }
    public string? Description { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public int SlotIntervalMin { get; set; }
    public bool IsActive { get; set; }
}