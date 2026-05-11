namespace BookingPlatform.Contracts.Auth;

public sealed class AuthResponse
{
    public long UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;

    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }

    public List<AuthBusinessMembershipDto> Memberships { get; set; } = new();
}