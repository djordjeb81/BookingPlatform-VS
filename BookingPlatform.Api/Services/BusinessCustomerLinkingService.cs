using BookingPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BookingPlatform.Api.Services;

public sealed class BusinessCustomerLinkingService : IBusinessCustomerLinkingService
{
    private readonly BookingDbContext _dbContext;

    public BusinessCustomerLinkingService(BookingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<BusinessCustomerLinkingResult> LinkByEmailAsync(
        long appUserId,
        string? email,
        CancellationToken cancellationToken)
    {
        var result = new BusinessCustomerLinkingResult();

        var normalizedEmail = NormalizeEmail(email);
        if (string.IsNullOrWhiteSpace(normalizedEmail))
            return result;

        var matchingProfiles = await _dbContext.CustomerProfiles
            .Where(x =>
                x.Email != null &&
                x.Email.Trim().ToLower() == normalizedEmail)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);

        if (matchingProfiles.Count == 0)
            return result;

        // Po novom pravilu: jedan email = jedan globalni CustomerProfile.
        // Ako iz nekog razloga postoje duplikati, ne povezujemo automatski.
        if (matchingProfiles.Count > 1)
        {
            result.SkippedDuplicateBusinessCount++;
            return result;
        }

        var profile = matchingProfiles[0];

        if (profile.AppUserId == appUserId)
        {
            result.AlreadyLinkedCount++;
            return result;
        }

        if (profile.AppUserId.HasValue && profile.AppUserId.Value != appUserId)
        {
            result.SkippedDuplicateBusinessCount++;
            return result;
        }

        var now = DateTime.UtcNow;

        profile.AppUserId = appUserId;
        profile.UpdatedAtUtc = now;

        // Privremeno sinhronizujemo stare kolone u business_customers,
        // dok ceo sistem potpuno ne pređe na CustomerProfile.
        var relatedBusinessCustomers = await _dbContext.BusinessCustomers
            .Where(x => x.CustomerProfileId == profile.Id)
            .ToListAsync(cancellationToken);

        foreach (var customer in relatedBusinessCustomers)
        {
            customer.AppUserId = appUserId;
            customer.FullName = profile.FullName;
            customer.Phone = profile.Phone;
            customer.Email = profile.Email;
            customer.UpdatedAtUtc = now;
        }

        result.LinkedCount = relatedBusinessCustomers.Count > 0 ? relatedBusinessCustomers.Count : 1;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return result;
    }

    private static string? NormalizeEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;

        return email.Trim().ToLowerInvariant();
    }
}