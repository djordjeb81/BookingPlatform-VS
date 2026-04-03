namespace BookingPlatform.Contracts.Businesses;

public sealed class BusinessDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int BusinessType { get; set; }
    public string? Description { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public bool IsActive { get; set; }
}