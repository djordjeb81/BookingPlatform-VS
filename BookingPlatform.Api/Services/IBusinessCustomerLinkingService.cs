namespace BookingPlatform.Api.Services;

public interface IBusinessCustomerLinkingService
{
    Task<BusinessCustomerLinkingResult> LinkByEmailAsync(
        long appUserId,
        string? email,
        CancellationToken cancellationToken);
}

public sealed class BusinessCustomerLinkingResult
{
    public int LinkedCount { get; set; }

    public int SkippedDuplicateBusinessCount { get; set; }

    public int AlreadyLinkedCount { get; set; }

    public bool HasAnyChange => LinkedCount > 0;
}