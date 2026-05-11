namespace BookingPlatform.Contracts.BusinessPortal;

public sealed class BusinessPortalBusinessDto
{
    public long BusinessId { get; set; }

    public string BusinessName { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? City { get; set; }
}