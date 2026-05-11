using BookingPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BookingPlatform.Api.Helpers;

public static class StaffServiceValidationHelper
{
    public static async Task<string?> ValidateStaffCanPerformServiceAsync(
        BookingDbContext dbContext,
        long businessId,
        long serviceId,
        long? staffMemberId,
        CancellationToken cancellationToken)
    {
        if (!staffMemberId.HasValue)
            return null;

        var staff = await dbContext.StaffMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == staffMemberId.Value, cancellationToken);

        if (staff is null)
            return "Izabrani radnik ne postoji.";

        if (staff.BusinessId != businessId)
            return "Izabrani radnik ne pripada ovoj radnji.";

        var service = await dbContext.Services
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == serviceId, cancellationToken);

        if (service is null)
            return "Izabrana usluga ne postoji.";

        if (service.BusinessId != businessId)
            return "Izabrana usluga ne pripada ovoj radnji.";

        var hasAssignment = await dbContext.StaffServiceAssignments
            .AsNoTracking()
            .AnyAsync(
                x => x.StaffMemberId == staffMemberId.Value &&
                     x.ServiceId == serviceId,
                cancellationToken);

        if (!hasAssignment)
            return "Izabrani radnik ne radi ovu uslugu.";

        return null;
    }
}