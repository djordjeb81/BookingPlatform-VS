using BookingPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BookingPlatform.Api.Helpers;

public static class StaffResourceValidationHelper
{
    public static async Task<string?> ValidateStaffCanUseResourceAsync(
        BookingDbContext dbContext,
        long businessId,
        long? staffMemberId,
        long? resourceId,
        CancellationToken cancellationToken)
    {
        if (!staffMemberId.HasValue || !resourceId.HasValue)
            return null;

        var staff = await dbContext.StaffMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == staffMemberId.Value, cancellationToken);

        if (staff is null)
            return "Izabrani radnik ne postoji.";

        if (staff.BusinessId != businessId)
            return "Izabrani radnik ne pripada ovoj radnji.";

        var resource = await dbContext.Resources
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == resourceId.Value, cancellationToken);

        if (resource is null)
            return "Izabrani resurs ne postoji.";

        if (resource.BusinessId != businessId)
            return "Izabrani resurs ne pripada ovoj radnji.";

        var hasAssignment = await dbContext.StaffResourceAssignments
            .AsNoTracking()
            .AnyAsync(
                x => x.StaffMemberId == staffMemberId.Value &&
                     x.ResourceId == resourceId.Value,
                cancellationToken);

        if (!hasAssignment)
            return "Izabrani radnik ne radi sa ovim resursom.";

        return null;
    }
}