using System.Security.Claims;
using BookingPlatform.Contracts.Common;
using BookingPlatform.Domain.Auth;
using BookingPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookingPlatform.Api.Controllers;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected BookingDbContext DbContext { get; }

    protected ApiControllerBase(BookingDbContext dbContext)
    {
        DbContext = dbContext;
    }

    protected long? TryGetCurrentUserId()
    {
        var rawUserId =
            User.FindFirstValue(ClaimTypes.NameIdentifier) ??
            User.FindFirstValue("sub") ??
            User.FindFirstValue("userId");

        return long.TryParse(rawUserId, out var userId) ? userId : null;
    }

    protected ActionResult BuildUnauthorized()
    {
        return Unauthorized(new ApiErrorResponse
        {
            Message = "Korisnik nije autentifikovan.",
            ReasonCode = "unauthenticated",
            ReasonCodes = new List<string> { "unauthenticated" }
        });
    }

    protected ActionResult BuildForbidden()
    {
        return StatusCode(StatusCodes.Status403Forbidden, new ApiErrorResponse
        {
            Message = "Korisnik nema dozvolu za ovu radnju.",
            ReasonCode = "forbidden",
            ReasonCodes = new List<string> { "forbidden" }
        });
    }

    protected async Task<ActionResult?> EnsureBusinessReadAccessAsync(
        long businessId,
        CancellationToken cancellationToken)
    {
        var userId = TryGetCurrentUserId();

        if (!userId.HasValue)
            return BuildUnauthorized();

        var membership = await DbContext.BusinessUserMemberships
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.AppUserId == userId.Value &&
                x.BusinessId == businessId &&
                x.IsActive,
                cancellationToken);

        if (membership is null)
            return BuildForbidden();

        return null;
    }

    protected async Task<ActionResult?> EnsureBusinessWriteAccessAsync(
        long businessId,
        CancellationToken cancellationToken)
    {
        var userId = TryGetCurrentUserId();

        if (!userId.HasValue)
            return BuildUnauthorized();

        var membership = await DbContext.BusinessUserMemberships
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.AppUserId == userId.Value &&
                x.BusinessId == businessId &&
                x.IsActive,
                cancellationToken);

        if (membership is null)
            return BuildForbidden();

        if (membership.Role is not BusinessUserRole.Owner and not BusinessUserRole.Manager)
            return BuildForbidden();

        return null;
    }

    protected async Task<ActionResult?> EnsureBusinessOwnerAccessAsync(
        long businessId,
        CancellationToken cancellationToken)
    {
        var userId = TryGetCurrentUserId();

        if (!userId.HasValue)
            return BuildUnauthorized();

        var membership = await DbContext.BusinessUserMemberships
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.AppUserId == userId.Value &&
                x.BusinessId == businessId &&
                x.IsActive,
                cancellationToken);

        if (membership is null)
            return BuildForbidden();

        if (membership.Role != BusinessUserRole.Owner)
            return BuildForbidden();

        return null;
    }

    protected async Task<List<long>> GetAccessibleBusinessIdsAsync(CancellationToken cancellationToken)
    {
        var userId = TryGetCurrentUserId();

        if (!userId.HasValue)
            return new List<long>();

        return await DbContext.BusinessUserMemberships
            .AsNoTracking()
            .Where(x => x.AppUserId == userId.Value && x.IsActive)
            .Select(x => x.BusinessId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }
}