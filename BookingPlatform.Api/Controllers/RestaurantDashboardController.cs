using BookingPlatform.Contracts.Common;
using BookingPlatform.Contracts.Restaurants;
using BookingPlatform.Domain.Restaurants;
using BookingPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookingPlatform.Api.Controllers;

[ApiController]
[Authorize]
[Produces("application/json")]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
[Route("api/[controller]")]
public sealed class RestaurantDashboardController : ApiControllerBase
{
    public RestaurantDashboardController(BookingDbContext dbContext) : base(dbContext)
    {
    }

    [HttpGet("summary")]
    public async Task<ActionResult<RestaurantDashboardSummaryDto>> GetSummary(
        [FromQuery] long businessId,
        CancellationToken cancellationToken)
    {
        if (businessId <= 0)
            return BadRequest("businessId je obavezan.");

        var accessResult = await EnsureBusinessReadAccessAsync(businessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var nowUtc = DateTime.UtcNow;
        var todayFromUtc = nowUtc.Date;
        var tomorrowFromUtc = todayFromUtc.AddDays(1);

        var activeTableSessionCount = await DbContext.RestaurantTableSessions
            .AsNoTracking()
            .CountAsync(x =>
                x.BusinessId == businessId &&
                x.Status == RestaurantTableSessionStatus.Active &&
                x.ReleasedAtUtc == null,
                cancellationToken);

        var pendingTableReservationCount = await DbContext.RestaurantTableReservations
            .AsNoTracking()
            .CountAsync(x =>
                x.BusinessId == businessId &&
                x.Status == RestaurantTableReservationStatus.PendingApproval,
                cancellationToken);

        var confirmedTableReservationTodayCount = await DbContext.RestaurantTableReservations
            .AsNoTracking()
            .CountAsync(x =>
                x.BusinessId == businessId &&
                x.Status == RestaurantTableReservationStatus.Confirmed &&
                x.ReservationAtUtc >= todayFromUtc &&
                x.ReservationAtUtc < tomorrowFromUtc,
                cancellationToken);

        var unassignedConfirmedTableReservationTodayCount = await DbContext.RestaurantTableReservations
            .AsNoTracking()
            .CountAsync(x =>
                x.BusinessId == businessId &&
                x.Status == RestaurantTableReservationStatus.Confirmed &&
                !x.TableResourceId.HasValue &&
                x.ReservationAtUtc >= todayFromUtc &&
                x.ReservationAtUtc < tomorrowFromUtc,
                cancellationToken);

        var activeKitchenOrderCount = await DbContext.RestaurantOrders
            .AsNoTracking()
            .CountAsync(x =>
                x.BusinessId == businessId &&
                (
                    x.Status == RestaurantOrderStatus.Submitted ||
                    x.Status == RestaurantOrderStatus.Preparing ||
                    x.Status == RestaurantOrderStatus.Ready
                ),
                cancellationToken);

        var todayTakeawayOrderCount = await DbContext.RestaurantOrders
            .AsNoTracking()
            .CountAsync(x =>
                x.BusinessId == businessId &&
                x.OrderType == RestaurantOrderType.Takeaway &&
                x.CreatedAtUtc >= todayFromUtc &&
                x.CreatedAtUtc < tomorrowFromUtc &&
                x.Status != RestaurantOrderStatus.Cancelled,
                cancellationToken);

        var todayDeliveryOrderCount = await DbContext.RestaurantOrders
            .AsNoTracking()
            .CountAsync(x =>
                x.BusinessId == businessId &&
                x.OrderType == RestaurantOrderType.Delivery &&
                x.CreatedAtUtc >= todayFromUtc &&
                x.CreatedAtUtc < tomorrowFromUtc &&
                x.Status != RestaurantOrderStatus.Cancelled,
                cancellationToken);

        var pendingAreaReservationCount = await DbContext.RestaurantAreaReservations
            .AsNoTracking()
            .CountAsync(x =>
                x.BusinessId == businessId &&
                x.Status == RestaurantAreaReservationStatus.PendingApproval,
                cancellationToken);

        var confirmedAreaReservationTodayCount = await DbContext.RestaurantAreaReservations
            .AsNoTracking()
            .CountAsync(x =>
                x.BusinessId == businessId &&
                x.Status == RestaurantAreaReservationStatus.Confirmed &&
                x.ReservationAtUtc >= todayFromUtc &&
                x.ReservationAtUtc < tomorrowFromUtc,
                cancellationToken);

        var activeAreaReservations = await DbContext.RestaurantAreaReservations
            .AsNoTracking()
            .Where(x =>
                x.BusinessId == businessId &&
                (
                    x.Status == RestaurantAreaReservationStatus.Confirmed ||
                    x.Status == RestaurantAreaReservationStatus.Arrived
                ))
            .Select(x => new
            {
                x.ReservationAtUtc,
                x.ExpectedDurationMin
            })
            .ToListAsync(cancellationToken);

        var activeAreaReservationCount = activeAreaReservations.Count(x =>
        {
            var durationMin = x.ExpectedDurationMin.GetValueOrDefault(240);

            if (durationMin <= 0)
                durationMin = 240;

            var endUtc = x.ReservationAtUtc.AddMinutes(durationMin);

            return x.ReservationAtUtc <= nowUtc && nowUtc < endUtc;
        });

        var todayOrders = await DbContext.RestaurantOrders
            .AsNoTracking()
            .Where(x =>
                x.BusinessId == businessId &&
                x.CreatedAtUtc >= todayFromUtc &&
                x.CreatedAtUtc < tomorrowFromUtc &&
                x.Status != RestaurantOrderStatus.Cancelled)
            .Select(x => new
            {
                x.TotalAmount,
                x.Currency
            })
            .ToListAsync(cancellationToken);

        var todayOrderTotalAmount = todayOrders.Sum(x => x.TotalAmount);
        var currency = todayOrders.FirstOrDefault()?.Currency ?? "RSD";

        return Ok(new RestaurantDashboardSummaryDto
        {
            BusinessId = businessId,
            StatusAtUtc = nowUtc,
            TodayFromUtc = todayFromUtc,
            TomorrowFromUtc = tomorrowFromUtc,
            ActiveTableSessionCount = activeTableSessionCount,
            PendingTableReservationCount = pendingTableReservationCount,
            ConfirmedTableReservationTodayCount = confirmedTableReservationTodayCount,
            UnassignedConfirmedTableReservationTodayCount = unassignedConfirmedTableReservationTodayCount,
            ActiveKitchenOrderCount = activeKitchenOrderCount,
            TodayTakeawayOrderCount = todayTakeawayOrderCount,
            TodayDeliveryOrderCount = todayDeliveryOrderCount,
            PendingAreaReservationCount = pendingAreaReservationCount,
            ConfirmedAreaReservationTodayCount = confirmedAreaReservationTodayCount,
            ActiveAreaReservationCount = activeAreaReservationCount,
            TodayOrderTotalAmount = todayOrderTotalAmount,
            Currency = currency
        });
    }
}