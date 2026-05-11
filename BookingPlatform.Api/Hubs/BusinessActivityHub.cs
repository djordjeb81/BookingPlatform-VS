using BookingPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BookingPlatform.Api.Hubs;

[Authorize]
public sealed class BusinessActivityHub : Hub
{
    private readonly BookingDbContext _dbContext;

    public BusinessActivityHub(BookingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public static string BusinessGroupName(long businessId)
    {
        return $"business-{businessId}";
    }

    public async Task JoinBusinessGroup(long businessId)
    {
        var userId = TryGetCurrentUserId();

        if (!userId.HasValue)
            throw new HubException("Korisnik nije prijavljen.");

        var hasAccess = await _dbContext.BusinessUserMemberships
            .AsNoTracking()
            .AnyAsync(x =>
                x.BusinessId == businessId &&
                x.AppUserId == userId.Value &&
                x.IsActive);

        if (!hasAccess)
            throw new HubException("Korisnik nema dozvolu za ovu radnju.");

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            BusinessGroupName(businessId));
    }

    private long? TryGetCurrentUserId()
    {
        var raw =
            Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? Context.User?.FindFirstValue("sub")
            ?? Context.User?.FindFirstValue("userId")
            ?? Context.User?.FindFirstValue("UserId")
            ?? Context.User?.FindFirstValue("nameid");

        if (long.TryParse(raw, out var userId))
            return userId;

        return null;
    }
}