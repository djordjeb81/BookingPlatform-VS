namespace BookingPlatform.Contracts.Auth;

public sealed class BusinessMembershipItemResponse
{
    public long MembershipId { get; set; }
    public long AppUserId { get; set; }
    public long BusinessId { get; set; }

    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}