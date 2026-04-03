namespace BookingPlatform.Contracts.Businesses;

public sealed class CreateBusinessRequest
{
    public string Name { get; set; } = string.Empty;
    public int BusinessType { get; set; }
    public string? Description { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
}