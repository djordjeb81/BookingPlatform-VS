using BookingPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BookingPlatform.Api.Services;

public sealed class BusinessCustomerCleanupService
{
    private readonly BookingDbContext _dbContext;
    private readonly ILogger<BusinessCustomerCleanupService> _logger;

    public BusinessCustomerCleanupService(
        BookingDbContext dbContext,
        ILogger<BusinessCustomerCleanupService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<int> CleanupRemovedBusinessCustomersAsync(
        TimeSpan olderThan,
        CancellationToken cancellationToken)
    {
        var cutoffUtc = DateTime.UtcNow.Subtract(olderThan);

        var candidates = await _dbContext.BusinessCustomers
            .Where(x =>
                !x.IsActive &&
                x.RemovedFromCustomerListAtUtc.HasValue &&
                x.RemovedFromCustomerListAtUtc.Value <= cutoffUtc)
            .OrderBy(x => x.Id)
            .Take(500)
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
            return 0;

        var candidateIds = candidates
            .Select(x => x.Id)
            .ToList();

        var usedBusinessCustomerIds = await _dbContext.Appointments
            .AsNoTracking()
            .Where(x =>
                x.BusinessCustomerId.HasValue &&
                candidateIds.Contains(x.BusinessCustomerId.Value))
            .Select(x => x.BusinessCustomerId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        var usedSet = usedBusinessCustomerIds.ToHashSet();

        var toDelete = candidates
            .Where(x => !usedSet.Contains(x.Id))
            .ToList();

        if (toDelete.Count == 0)
            return 0;

        _dbContext.BusinessCustomers.RemoveRange(toDelete);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Cleaned up {Count} removed business-customer links older than {OlderThanDays} days.",
            toDelete.Count,
            olderThan.TotalDays);

        return toDelete.Count;
    }
}