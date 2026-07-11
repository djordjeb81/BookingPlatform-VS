namespace BookingPlatform.Contracts.Fitness;

public sealed class UpdateFitnessMemberRequest
{
    public long? CustomerProfileId { get; set; }

    public long? BusinessCustomerId { get; set; }

    public long? AppUserId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? MemberCode { get; set; }

    public bool IsActive { get; set; }
}