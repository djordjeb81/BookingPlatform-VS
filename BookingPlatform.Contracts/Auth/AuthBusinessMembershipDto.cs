namespace BookingPlatform.Contracts.Auth;

public sealed class AuthBusinessMembershipDto
{
    public long BusinessId { get; set; }
    public string BusinessName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}