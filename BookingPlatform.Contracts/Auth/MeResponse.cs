namespace BookingPlatform.Contracts.Auth;

public sealed class MeResponse
{
    public long UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; }

    public List<AuthBusinessMembershipDto> Memberships { get; set; } = new();
}