namespace BookingPlatform.Contracts.Auth;

public sealed class SetBusinessMembershipActiveRequest
{
    public long BusinessId { get; set; }
    public long MembershipId { get; set; }
    public bool IsActive { get; set; }
}