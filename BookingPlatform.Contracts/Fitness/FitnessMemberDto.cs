namespace BookingPlatform.Contracts.Fitness;

public sealed class FitnessMemberDto
{
    public long Id { get; set; }

    public long BusinessId { get; set; }

    public long? CustomerProfileId { get; set; }

    public long? BusinessCustomerId { get; set; }

    public long? AppUserId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? MemberCode { get; set; }

    public bool IsActive { get; set; }

    public bool HasActiveMembership { get; set; }

    public DateOnly? ActiveMembershipUntil { get; set; }

    public string MembershipStatusText { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}