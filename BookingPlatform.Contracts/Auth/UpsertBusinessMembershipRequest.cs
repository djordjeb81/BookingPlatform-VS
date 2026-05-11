namespace BookingPlatform.Contracts.Auth;

public sealed class UpsertBusinessMembershipRequest
{
    public long BusinessId { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}