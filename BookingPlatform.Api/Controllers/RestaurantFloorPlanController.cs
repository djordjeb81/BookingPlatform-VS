using BookingPlatform.Contracts.Common;
using BookingPlatform.Contracts.Restaurants;
using BookingPlatform.Domain.Resources;
using BookingPlatform.Domain.Restaurants;
using BookingPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BookingPlatform.Api.Services;
using BookingPlatform.Contracts.CustomerPortal;

namespace BookingPlatform.Api.Controllers;

[ApiController]
[Authorize]
[Produces("application/json")]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
[Route("api/[controller]")]
public sealed class RestaurantFloorPlanController : ApiControllerBase
{
    private const int ReservationSoftWarningMinutes = 30;
    private const int ReservationReleaseBeforeMinutes = 10;
    private const int ReservationLookupDays = 7;
    private const int ReservationLookupBackHours = 8;
    private const int ReservationAutoNoShowGraceMinutes = 30;
    private const int MaxUpcomingReservationsPerTable = 5;

    private readonly ISystemAlarmService _systemAlarmService;

    public RestaurantFloorPlanController(
        BookingDbContext dbContext,
        ISystemAlarmService systemAlarmService)
        : base(dbContext)
    {
        _systemAlarmService = systemAlarmService;
    }

    [HttpGet("area/{restaurantAreaId:long}")]
    public async Task<ActionResult<RestaurantFloorPlanDto>> GetAreaFloorPlan(
       [FromRoute] long restaurantAreaId,
       CancellationToken cancellationToken)
    {
        var statusAtUtc = DateTime.UtcNow;

        var area = await DbContext.RestaurantAreas
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == restaurantAreaId, cancellationToken);

        if (area is null)
            return NotFound("Sala ne postoji.");

        var accessResult = await EnsureBusinessReadAccessAsync(area.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        await MarkExpiredConfirmedTableReservationsNoShowAsync(
            businessId: area.BusinessId,
            restaurantAreaId: restaurantAreaId,
            statusAtUtc: statusAtUtc,
            cancellationToken: cancellationToken);

        var result = await BuildAreaFloorPlanDtoAsync(
            area,
            statusAtUtc,
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("area/{restaurantAreaId:long}/customer")]
    [ProducesResponseType(typeof(CustomerRestaurantFloorPlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerRestaurantFloorPlanDto>> GetCustomerAreaFloorPlan(
    [FromRoute] long restaurantAreaId,
    CancellationToken cancellationToken)
    {
        var statusAtUtc = DateTime.UtcNow;

        var area = await DbContext.RestaurantAreas
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == restaurantAreaId, cancellationToken);

        if (area is null)
            return NotFound("Sala ne postoji.");

        var accessResult = await EnsureCustomerBusinessAccessAsync(area.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var fullFloorPlan = await BuildAreaFloorPlanDtoAsync(
            area,
            statusAtUtc,
            cancellationToken);

        return Ok(ToCustomerFloorPlanDto(fullFloorPlan));
    }

    [HttpGet("business/{businessId:long}/areas/customer")]
    [ProducesResponseType(typeof(List<CustomerRestaurantAreaDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<CustomerRestaurantAreaDto>>> GetCustomerRestaurantAreas(
    [FromRoute] long businessId,
    CancellationToken cancellationToken)
    {
        if (businessId <= 0)
            return BadRequest("businessId je obavezan.");

        var accessResult = await EnsureCustomerBusinessAccessAsync(businessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var items = await DbContext.RestaurantAreas
            .AsNoTracking()
            .Where(x =>
                x.BusinessId == businessId &&
                x.IsActive)
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Name)
            .Select(x => new CustomerRestaurantAreaDto
            {
                Id = x.Id,
                BusinessId = x.BusinessId,
                Name = x.Name,
                DisplayOrder = x.DisplayOrder
            })
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpGet("area/{restaurantAreaId:long}/table-schedule/customer")]
    [ProducesResponseType(typeof(List<CustomerRestaurantTableScheduleItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<CustomerRestaurantTableScheduleItemDto>>> GetCustomerTableSchedule(
    [FromRoute] long restaurantAreaId,
    [FromQuery] long tableResourceId,
    [FromQuery] DateOnly date,
    CancellationToken cancellationToken)
    {
        if (restaurantAreaId <= 0)
            return BadRequest("restaurantAreaId je obavezan.");

        if (tableResourceId <= 0)
            return BadRequest("tableResourceId je obavezan.");

        var area = await DbContext.RestaurantAreas
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == restaurantAreaId, cancellationToken);

        if (area is null)
            return NotFound("Sala ne postoji.");

        var accessResult = await EnsureCustomerBusinessAccessAsync(area.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var tableExists = await DbContext.Resources
            .AsNoTracking()
            .AnyAsync(x =>
                x.Id == tableResourceId &&
                x.BusinessId == area.BusinessId &&
                x.RestaurantAreaId == restaurantAreaId &&
                x.IsActive &&
                (
                    x.ResourceType == ResourceType.Table ||
                    x.ResourceType == ResourceType.DiningTable
                ),
                cancellationToken);

        if (!tableExists)
            return BadRequest("Izabrani sto ne postoji ili ne pripada izabranoj sali.");

        var dayStartUtc = DateTime.SpecifyKind(
            date.ToDateTime(TimeOnly.MinValue),
            DateTimeKind.Utc);

        var dayEndUtc = dayStartUtc.AddDays(1);

        var result = new List<CustomerRestaurantTableScheduleItemDto>();

        var reservations = await DbContext.RestaurantTableReservations
            .AsNoTracking()
            .Where(x =>
                x.BusinessId == area.BusinessId &&
                x.RestaurantAreaId == restaurantAreaId &&
                x.TableResourceId == tableResourceId &&
(
    x.Status == RestaurantTableReservationStatus.PendingApproval ||
    x.Status == RestaurantTableReservationStatus.Confirmed
) &&
                x.ReservationAtUtc < dayEndUtc)
            .Select(x => new
            {
                x.ReservationAtUtc,
                x.ExpectedDurationMin
            })
            .ToListAsync(cancellationToken);

        foreach (var reservation in reservations)
        {
            var fromUtc = EnsureUtc(reservation.ReservationAtUtc);

            var durationMin = reservation.ExpectedDurationMin.GetValueOrDefault(120);
            if (durationMin <= 0)
                durationMin = 120;

            var untilUtc = fromUtc.AddMinutes(durationMin);

            var overlapsSelectedDate = fromUtc < dayEndUtc && untilUtc > dayStartUtc;
            if (!overlapsSelectedDate)
                continue;

            result.Add(new CustomerRestaurantTableScheduleItemDto
            {
                FromUtc = fromUtc,
                UntilUtc = untilUtc,
                Status = 2,
                StatusText = "Rezervisano"
            });
        }

        var sessions = await DbContext.RestaurantTableSessions
            .AsNoTracking()
            .Where(x =>
                x.BusinessId == area.BusinessId &&
                x.RestaurantAreaId == restaurantAreaId &&
                x.TableResourceId == tableResourceId &&
                x.Status == RestaurantTableSessionStatus.Active &&
                x.ReleasedAtUtc == null)
            .Select(x => new
            {
                x.StartedAtUtc
            })
            .ToListAsync(cancellationToken);

        foreach (var session in sessions)
        {
            var fromUtc = EnsureUtc(session.StartedAtUtc);

            result.Add(new CustomerRestaurantTableScheduleItemDto
            {
                FromUtc = fromUtc,
                UntilUtc = DateTime.UtcNow,
                Status = 1,
                StatusText = "Trenutno zauzeto"
            });
        }

        return Ok(result
            .OrderBy(x => x.FromUtc)
            .ToList());
    }

    private async Task<RestaurantFloorPlanDto> BuildAreaFloorPlanDtoAsync(
    RestaurantArea area,
    DateTime statusAtUtc,
    CancellationToken cancellationToken)
    {
        var restaurantAreaId = area.Id;

        var elements = await DbContext.RestaurantLayoutElements
            .AsNoTracking()
            .Where(x => x.RestaurantAreaId == restaurantAreaId && x.IsActive)
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Id)
            .Select(x => new RestaurantLayoutElementDto
            {
                Id = x.Id,
                RestaurantAreaId = x.RestaurantAreaId,
                ElementType = (int)x.ElementType,
                Label = x.Label,
                X = x.X,
                Y = x.Y,
                Width = x.Width,
                Height = x.Height,
                RotationDeg = x.RotationDeg,
                ShapeType = (int)x.ShapeType,
                PointsJson = x.PointsJson,
                IsObstacle = x.IsObstacle,
                DisplayOrder = x.DisplayOrder,
                IsActive = x.IsActive
            })
            .ToListAsync(cancellationToken);

        var resources = await DbContext.Resources
            .AsNoTracking()
            .Where(x =>
                x.BusinessId == area.BusinessId &&
                x.RestaurantAreaId == restaurantAreaId)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        var resourceIds = resources
            .Select(x => x.Id)
            .ToList();

        var activeSessions = await DbContext.RestaurantTableSessions
            .AsNoTracking()
            .Where(x =>
                x.BusinessId == area.BusinessId &&
                x.RestaurantAreaId == restaurantAreaId &&
                resourceIds.Contains(x.TableResourceId) &&
                x.Status == RestaurantTableSessionStatus.Active &&
                x.ReleasedAtUtc == null)
            .OrderByDescending(x => x.StartedAtUtc)
            .ToListAsync(cancellationToken);

        var activeSessionByTableId = activeSessions
            .GroupBy(x => x.TableResourceId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.StartedAtUtc).First());

        var reservationLookupToUtc = statusAtUtc.AddDays(ReservationLookupDays);

        var upcomingAreaReservations = await DbContext.RestaurantAreaReservations
            .AsNoTracking()
            .Where(x =>
                x.BusinessId == area.BusinessId &&
                x.RestaurantAreaId == restaurantAreaId &&
                x.Status == RestaurantAreaReservationStatus.Confirmed &&
                x.ReservationAtUtc <= reservationLookupToUtc)
            .OrderBy(x => x.ReservationAtUtc)
            .Take(20)
            .ToListAsync(cancellationToken);

        var currentAreaReservation = upcomingAreaReservations
            .FirstOrDefault(x =>
            {
                var startUtc = x.ReservationAtUtc;
                var durationMin = x.ExpectedDurationMin.GetValueOrDefault(240);

                if (durationMin <= 0)
                    durationMin = 240;

                var endUtc = startUtc.AddMinutes(durationMin);

                return startUtc <= statusAtUtc && statusAtUtc < endUtc;
            });

        var nextAreaReservation = upcomingAreaReservations
            .Where(x => x.ReservationAtUtc >= statusAtUtc)
            .OrderBy(x => x.ReservationAtUtc)
            .FirstOrDefault();

        var reservationLookupFromUtc = statusAtUtc.AddHours(-ReservationLookupBackHours);

        var tableReservationsForDisplay = await DbContext.RestaurantTableReservations
            .AsNoTracking()
            .Where(x =>
                x.BusinessId == area.BusinessId &&
                x.RestaurantAreaId == restaurantAreaId &&
                x.TableResourceId.HasValue &&
                resourceIds.Contains(x.TableResourceId.Value) &&
                (
    x.Status == RestaurantTableReservationStatus.PendingApproval ||
    x.Status == RestaurantTableReservationStatus.Confirmed
) &&
                x.ReservationAtUtc >= reservationLookupFromUtc &&
                x.ReservationAtUtc <= reservationLookupToUtc)
            .OrderBy(x => x.ReservationAtUtc)
            .ToListAsync(cancellationToken);

        var upcomingReservations = tableReservationsForDisplay
            .Where(x =>
            {
                var durationMin = x.ExpectedDurationMin.GetValueOrDefault(120);

                if (durationMin <= 0)
                    durationMin = 120;

                var reservationEndUtc = x.ReservationAtUtc.AddMinutes(durationMin);

                return reservationEndUtc > statusAtUtc;
            })
            .ToList();

        var unassignedUpcomingReservations = await DbContext.RestaurantTableReservations
            .AsNoTracking()
            .Where(x =>
                x.BusinessId == area.BusinessId &&
                x.RestaurantAreaId == restaurantAreaId &&
                !x.TableResourceId.HasValue &&
                x.Status == RestaurantTableReservationStatus.Confirmed &&
                x.ReservationAtUtc >= statusAtUtc &&
                x.ReservationAtUtc <= reservationLookupToUtc)
            .OrderBy(x => x.ReservationAtUtc)
            .Take(50)
            .ToListAsync(cancellationToken);

        var nextReservationByTableId = upcomingReservations
            .GroupBy(x => x.TableResourceId!.Value)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(x => x.ReservationAtUtc).First());

        var upcomingReservationsByTableId = upcomingReservations
            .GroupBy(x => x.TableResourceId!.Value)
            .ToDictionary(
                g => g.Key,
                g => g
                    .OrderBy(x => x.ReservationAtUtc)
                    .Take(MaxUpcomingReservationsPerTable)
                    .ToList());

        var activeSessionIds = activeSessions
            .Select(x => x.Id)
            .ToList();

        var allOrders = activeSessionIds.Count == 0
            ? new List<RestaurantOrder>()
            : await DbContext.RestaurantOrders
                .AsNoTracking()
                .Where(x =>
                    x.TableSessionId.HasValue &&
                    activeSessionIds.Contains(x.TableSessionId.Value) &&
                    x.Status != RestaurantOrderStatus.Cancelled)
                .OrderByDescending(x => x.CreatedAtUtc)
                .ToListAsync(cancellationToken);

        var activeOrders = allOrders
            .Where(x => x.Status != RestaurantOrderStatus.Served)
            .ToList();

        var allOrdersBySessionId = allOrders
            .Where(x => x.TableSessionId.HasValue)
            .GroupBy(x => x.TableSessionId!.Value)
            .ToDictionary(
                g => g.Key,
                g => g.ToList());

        var activeOrdersBySessionId = activeOrders
            .Where(x => x.TableSessionId.HasValue)
            .GroupBy(x => x.TableSessionId!.Value)
            .ToDictionary(
                g => g.Key,
                g => g.ToList());

        var payments = activeSessionIds.Count == 0
            ? new List<RestaurantPayment>()
            : await DbContext.RestaurantPayments
                .AsNoTracking()
                .Where(x =>
                    activeSessionIds.Contains(x.TableSessionId) &&
                    x.Status == RestaurantPaymentStatus.Paid)
                .ToListAsync(cancellationToken);

        var paidAmountBySessionId = payments
            .GroupBy(x => x.TableSessionId)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(x => x.Amount));

        var areaStatus = GetAreaStatus(
            area.IsActive,
            currentAreaReservation,
            nextAreaReservation,
            statusAtUtc);

        var resourceDtos = resources
            .Select(resource =>
            {
                activeSessionByTableId.TryGetValue(resource.Id, out var activeSession);
                nextReservationByTableId.TryGetValue(resource.Id, out var nextReservation);
                upcomingReservationsByTableId.TryGetValue(resource.Id, out var resourceUpcomingReservations);

                resourceUpcomingReservations ??= new List<RestaurantTableReservation>();

                var activeOrdersForSession = new List<RestaurantOrder>();
                var allOrdersForSession = new List<RestaurantOrder>();
                var paidAmount = 0m;

                if (activeSession is not null)
                {
                    activeOrdersBySessionId.TryGetValue(activeSession.Id, out activeOrdersForSession);
                    allOrdersBySessionId.TryGetValue(activeSession.Id, out allOrdersForSession);
                    paidAmountBySessionId.TryGetValue(activeSession.Id, out paidAmount);
                }

                return ToResourceDto(
                    resource,
                    activeSession,
                    nextReservation,
                    resourceUpcomingReservations,
                    statusAtUtc,
                    activeOrdersForSession ?? new List<RestaurantOrder>(),
                    allOrdersForSession ?? new List<RestaurantOrder>(),
                    paidAmount,
                    areaStatus);
            })
            .ToList();

        var result = new RestaurantFloorPlanDto
        {
            BusinessId = area.BusinessId,
            RestaurantAreaId = area.Id,
            AreaName = area.Name,
            CanvasWidth = area.CanvasWidth,
            CanvasHeight = area.CanvasHeight,
            BoundaryPointsJson = area.BoundaryPointsJson,
            IsAreaActive = area.IsActive,
            IsReservableAsWhole = area.IsReservableAsWhole,
            WholeAreaResourceId = area.WholeAreaResourceId,
            AreaStatus = (int)areaStatus,
            AreaStatusText = GetAreaStatusText(areaStatus),
            StatusAtUtc = statusAtUtc,

            CurrentAreaReservationId = currentAreaReservation?.Id,
            CurrentAreaReservationStartedAtUtc = currentAreaReservation?.ReservationAtUtc,
            CurrentAreaReservationEndsAtUtc = currentAreaReservation is null
                ? null
                : currentAreaReservation.ReservationAtUtc.AddMinutes(
                    currentAreaReservation.ExpectedDurationMin.GetValueOrDefault(240) <= 0
                        ? 240
                        : currentAreaReservation.ExpectedDurationMin.GetValueOrDefault(240)),
            CurrentAreaReservationCustomerName = currentAreaReservation?.CustomerName,
            CurrentAreaReservationPartySize = currentAreaReservation?.PartySize,

            NextAreaReservationId = nextAreaReservation?.Id,
            NextAreaReservationAtUtc = nextAreaReservation?.ReservationAtUtc,
            NextAreaReservationCustomerName = nextAreaReservation?.CustomerName,
            NextAreaReservationPartySize = nextAreaReservation?.PartySize,

            AreaReservationWarningText = BuildAreaReservationWarningText(
                currentAreaReservation,
                nextAreaReservation,
                statusAtUtc),

            UpcomingAreaReservations = upcomingAreaReservations
                .Select(ToAreaReservationSummaryDto)
                .ToList(),

            UnassignedUpcomingReservationCount = unassignedUpcomingReservations.Count,
            NextUnassignedReservationAtUtc = unassignedUpcomingReservations
                .OrderBy(x => x.ReservationAtUtc)
                .FirstOrDefault()
                ?.ReservationAtUtc,

            UnassignedUpcomingReservations = unassignedUpcomingReservations
                .Select(ToUnassignedReservationDto)
                .ToList(),

            Elements = elements,
            Resources = resourceDtos
        };

        return result;
    }

    private async Task MarkExpiredConfirmedTableReservationsNoShowAsync(
      long businessId,
      long restaurantAreaId,
      DateTime statusAtUtc,
      CancellationToken cancellationToken)
    {
        var autoNoShowBeforeUtc = statusAtUtc.AddMinutes(-ReservationAutoNoShowGraceMinutes);

        var candidates = await DbContext.RestaurantTableReservations
            .Where(x =>
                x.BusinessId == businessId &&
                x.RestaurantAreaId == restaurantAreaId &&
                x.Status == RestaurantTableReservationStatus.Confirmed &&
                x.ReservationAtUtc <= autoNoShowBeforeUtc)
            .ToListAsync(cancellationToken);

        var changedReservationIds = new List<long>();

        foreach (var reservation in candidates)
        {
            var durationMin = reservation.ExpectedDurationMin.GetValueOrDefault(120);

            if (durationMin <= 0)
                durationMin = 120;

            var reservationEndUtc = reservation.ReservationAtUtc.AddMinutes(durationMin);

            if (reservationEndUtc > autoNoShowBeforeUtc)
                continue;

            if (reservation.ArrivedAtUtc.HasValue)
                continue;

            if (reservation.CreatedTableSessionId.HasValue)
                continue;

            reservation.Status = RestaurantTableReservationStatus.NoShow;
            reservation.UpdatedAtUtc = statusAtUtc;
            reservation.InternalNote = AppendInternalNote(
                reservation.InternalNote,
                "Automatski označeno kao nedolazak jer je termin prošao bez evidentiranog dolaska.");

            changedReservationIds.Add(reservation.Id);
        }

        if (changedReservationIds.Count == 0)
            return;

        await DbContext.SaveChangesAsync(cancellationToken);

        foreach (var reservationId in changedReservationIds)
        {
            await _systemAlarmService.CancelRestaurantTableShouldBeFreeAlarmForReservationAsync(
                businessId,
                reservationId,
                cancellationToken);
        }
    }

    private static string AppendInternalNote(string? existingNote, string note)
    {
        if (string.IsNullOrWhiteSpace(existingNote))
            return note;

        return existingNote + Environment.NewLine + note;
    }
    private static RestaurantAreaReservationSummaryDto ToAreaReservationSummaryDto(
    RestaurantAreaReservation entity)
    {
        return new RestaurantAreaReservationSummaryDto
        {
            ReservationId = entity.Id,
            BusinessId = entity.BusinessId,
            RestaurantAreaId = entity.RestaurantAreaId,
            PartySize = entity.PartySize,
            CustomerName = entity.CustomerName,
            CustomerPhone = entity.CustomerPhone,
            ReservationAtUtc = entity.ReservationAtUtc,
            ExpectedDurationMin = entity.ExpectedDurationMin,
            Status = (int)entity.Status,
            StatusText = GetAreaReservationStatusText(entity.Status),
            Note = entity.Note,
            InternalNote = entity.InternalNote
        };
    }

    private static string? BuildAreaReservationWarningText(
        RestaurantAreaReservation? currentAreaReservation,
        RestaurantAreaReservation? nextAreaReservation,
        DateTime statusAtUtc)
    {
        if (currentAreaReservation is not null)
        {
            var durationMin = currentAreaReservation.ExpectedDurationMin.GetValueOrDefault(240);

            if (durationMin <= 0)
                durationMin = 240;

            var endUtc = currentAreaReservation.ReservationAtUtc.AddMinutes(durationMin);

            var endLocal = DateTime.SpecifyKind(
                    endUtc,
                    DateTimeKind.Utc)
                .ToLocalTime();

            return $"Cela sala je trenutno rezervisana do {endLocal:HH:mm}.";
        }

        if (nextAreaReservation is null)
            return null;

        var reservationAtLocal = DateTime.SpecifyKind(
                nextAreaReservation.ReservationAtUtc,
                DateTimeKind.Utc)
            .ToLocalTime();

        var mustBeFreeByLocal = DateTime.SpecifyKind(
                nextAreaReservation.ReservationAtUtc.AddMinutes(-ReservationReleaseBeforeMinutes),
                DateTimeKind.Utc)
            .ToLocalTime();

        var minutesToReservation = (nextAreaReservation.ReservationAtUtc - statusAtUtc).TotalMinutes;
        var reservationTimeText = reservationAtLocal.ToString("HH:mm");
        var mustBeFreeByText = mustBeFreeByLocal.ToString("HH:mm");

        if (minutesToReservation <= ReservationSoftWarningMinutes)
        {
            return $"Cela sala je rezervisana uskoro u {reservationTimeText}. Sala treba da bude slobodna najkasnije do {mustBeFreeByText}.";
        }

        return $"Cela sala ima rezervaciju u {reservationTimeText}.";
    }

    private static string GetAreaReservationStatusText(RestaurantAreaReservationStatus status)
    {
        return status switch
        {
            RestaurantAreaReservationStatus.PendingApproval => "Čeka potvrdu",
            RestaurantAreaReservationStatus.Confirmed => "Potvrđeno",
            RestaurantAreaReservationStatus.Rejected => "Odbijeno",
            RestaurantAreaReservationStatus.Cancelled => "Otkazano",
            RestaurantAreaReservationStatus.Arrived => "Došli",
            RestaurantAreaReservationStatus.NoShow => "Nisu došli",
            RestaurantAreaReservationStatus.Completed => "Završeno",
            _ => "Nepoznat status"
        };
    }

    private static RestaurantUnassignedReservationDto ToUnassignedReservationDto(
    RestaurantTableReservation entity)
    {
        return new RestaurantUnassignedReservationDto
        {
            ReservationId = entity.Id,
            BusinessId = entity.BusinessId,
            RestaurantAreaId = entity.RestaurantAreaId,
            PartySize = entity.PartySize,
            CustomerName = entity.CustomerName,
            CustomerPhone = entity.CustomerPhone,
            ReservationAtUtc = entity.ReservationAtUtc,
            ExpectedDurationMin = entity.ExpectedDurationMin,
            Status = (int)entity.Status,
            StatusText = GetReservationStatusText(entity.Status),
            Note = entity.Note,
            InternalNote = entity.InternalNote
        };
    }

    private static RestaurantTableReservationPreviewDto ToReservationPreviewDto(
    RestaurantTableReservation entity)
    {
        return new RestaurantTableReservationPreviewDto
        {
            ReservationId = entity.Id,
            ReservationAtUtc = entity.ReservationAtUtc,
            PartySize = entity.PartySize,
            CustomerName = entity.CustomerName,
            CustomerPhone = entity.CustomerPhone,
            Status = (int)entity.Status,
            StatusText = GetReservationStatusText(entity.Status),
            ExpectedDurationMin = entity.ExpectedDurationMin
        };
    }

    private static RestaurantAreaVisualStatusDto GetAreaStatus(
        bool isAreaActive,
        RestaurantAreaReservation? currentAreaReservation,
        RestaurantAreaReservation? nextAreaReservation,
        DateTime statusAtUtc)
    {
        if (!isAreaActive)
            return RestaurantAreaVisualStatusDto.Inactive;

        if (currentAreaReservation is not null)
            return RestaurantAreaVisualStatusDto.Occupied;

        if (nextAreaReservation is not null)
        {
            var minutesToReservation = (nextAreaReservation.ReservationAtUtc - statusAtUtc).TotalMinutes;

            if (minutesToReservation <= ReservationSoftWarningMinutes)
                return RestaurantAreaVisualStatusDto.PendingReservation;
        }

        return RestaurantAreaVisualStatusDto.Available;
    }

    private static RestaurantFloorPlanResourceDto ToResourceDto(
        Resource resource,
        RestaurantTableSession? activeSession,
        RestaurantTableReservation? nextReservation,
        List<RestaurantTableReservation> upcomingReservations,
        DateTime statusAtUtc,
        List<RestaurantOrder> activeOrders,
        List<RestaurantOrder> allOrders,
        decimal paidAmount,
        RestaurantAreaVisualStatusDto areaStatus)
    {
        var status = GetResourceStatus(resource, activeSession, nextReservation, statusAtUtc, areaStatus);
        var latestOrder = activeOrders
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefault();

        var billTotalAmount = allOrders.Sum(x => x.TotalAmount);
        var remainingAmount = billTotalAmount - paidAmount;

        if (remainingAmount < 0)
            remainingAmount = 0;

        return new RestaurantFloorPlanResourceDto
        {
            ResourceId = resource.Id,
            BusinessId = resource.BusinessId,
            Name = resource.Name,
            ResourceType = (int)resource.ResourceType,
            Capacity = resource.Capacity,
            IsActive = resource.IsActive,
            ResourceGroupId = resource.ResourceGroupId,
            RestaurantAreaId = resource.RestaurantAreaId,
            LayoutX = resource.LayoutX,
            LayoutY = resource.LayoutY,
            LayoutWidth = resource.LayoutWidth,
            LayoutHeight = resource.LayoutHeight,
            LayoutRotationDeg = resource.LayoutRotationDeg,
            LayoutShape = (int)resource.LayoutShape,
            LayoutPointsJson = resource.LayoutPointsJson,
            Status = (int)status,
            StatusText = GetResourceStatusText(status),
            CurrentTableSessionId = activeSession?.Id,
            CurrentAppointmentId = null,
            OccupiedFromUtc = activeSession?.StartedAtUtc,
            PartySize = activeSession?.PartySize,
            CustomerName = activeSession?.CustomerName,
            NextReservationAtUtc = nextReservation?.ReservationAtUtc,
            NextReservationId = nextReservation?.Id,
            NextReservationCustomerName = nextReservation?.CustomerName,
            NextReservationPartySize = nextReservation?.PartySize,
            UpcomingReservations = upcomingReservations
    .OrderBy(x => x.ReservationAtUtc)
    .Select(ToReservationPreviewDto)
    .ToList(),
            MustBeFreeByUtc = nextReservation?.ReservationAtUtc.AddMinutes(-ReservationReleaseBeforeMinutes),
            ReservationWarningText = BuildReservationWarningText(nextReservation, statusAtUtc),
            ActiveOrderCount = activeOrders.Count,
            HasActiveOrders = activeOrders.Count > 0,
            ActiveOrderTotalAmount = activeOrders.Sum(x => x.TotalAmount),
            LatestOrderStatus = latestOrder is null ? null : (int)latestOrder.Status,
            LatestOrderStatusText = latestOrder is null ? null : GetOrderStatusText(latestOrder.Status),
            BillTotalAmount = billTotalAmount,
            BillPaidAmount = paidAmount,
            BillRemainingAmount = remainingAmount,
            IsBillFullyPaid = billTotalAmount > 0 && paidAmount >= billTotalAmount
        };
    }

    private static RestaurantResourceVisualStatusDto GetResourceStatus(
        Resource resource,
        RestaurantTableSession? activeSession,
        RestaurantTableReservation? nextReservation,
        DateTime statusAtUtc,
        RestaurantAreaVisualStatusDto areaStatus)
    {
        if (!resource.IsActive)
            return RestaurantResourceVisualStatusDto.Inactive;

        if (areaStatus is RestaurantAreaVisualStatusDto.Occupied or RestaurantAreaVisualStatusDto.PendingReservation)
            return RestaurantResourceVisualStatusDto.AreaOccupied;

        if (activeSession is not null)
            return RestaurantResourceVisualStatusDto.Occupied;

        if (nextReservation is not null)
        {
            var durationMin = nextReservation.ExpectedDurationMin.GetValueOrDefault(120);

            if (durationMin <= 0)
                durationMin = 120;

            var reservationEndUtc = nextReservation.ReservationAtUtc.AddMinutes(durationMin);

            if (nextReservation.ReservationAtUtc <= statusAtUtc && statusAtUtc < reservationEndUtc)
                return RestaurantResourceVisualStatusDto.ReservedLater;

            var minutesToReservation = (nextReservation.ReservationAtUtc - statusAtUtc).TotalMinutes;

            if (minutesToReservation <= ReservationSoftWarningMinutes)
                return RestaurantResourceVisualStatusDto.ReservedLater;
        }

        return RestaurantResourceVisualStatusDto.Available;
    }
    private static string? BuildReservationWarningText(
        RestaurantTableReservation? nextReservation,
        DateTime statusAtUtc)
    {
        if (nextReservation is null)
            return null;

        var durationMin = nextReservation.ExpectedDurationMin.GetValueOrDefault(120);

        if (durationMin <= 0)
            durationMin = 120;

        var reservationEndUtc = nextReservation.ReservationAtUtc.AddMinutes(durationMin);

        var reservationAtLocal = DateTime.SpecifyKind(
                nextReservation.ReservationAtUtc,
                DateTimeKind.Utc)
            .ToLocalTime();

        var reservationEndLocal = DateTime.SpecifyKind(
                reservationEndUtc,
                DateTimeKind.Utc)
            .ToLocalTime();

        var mustBeFreeByLocal = DateTime.SpecifyKind(
                nextReservation.ReservationAtUtc.AddMinutes(-ReservationReleaseBeforeMinutes),
                DateTimeKind.Utc)
            .ToLocalTime();

        if (nextReservation.ReservationAtUtc <= statusAtUtc && statusAtUtc < reservationEndUtc)
        {
            return $"Sto ima rezervaciju koja je u toku do {reservationEndLocal:HH:mm}.";
        }

        var minutesToReservation = (nextReservation.ReservationAtUtc - statusAtUtc).TotalMinutes;
        var reservationTimeText = reservationAtLocal.ToString("HH:mm");
        var mustBeFreeByText = mustBeFreeByLocal.ToString("HH:mm");

        if (minutesToReservation <= ReservationSoftWarningMinutes)
        {
            return $"Sto je rezervisan uskoro u {reservationTimeText}. Treba da bude slobodan najkasnije do {mustBeFreeByText}.";
        }

        return $"Sto ima rezervaciju u {reservationTimeText}. Ako ga zauzmete, treba ga osloboditi najkasnije do {mustBeFreeByText}.";
    }

    private async Task<ActionResult?> EnsureCustomerBusinessAccessAsync(
    long businessId,
    CancellationToken cancellationToken)
    {
        var userId = TryGetCurrentUserId();

        if (!userId.HasValue)
            return Unauthorized("Token nije validan.");

        var profile = await DbContext.CustomerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.AppUserId == userId.Value, cancellationToken);

        if (profile is null)
            return Forbid();

        var hasAccess = await DbContext.BusinessCustomers
            .AsNoTracking()
            .AnyAsync(x =>
                x.BusinessId == businessId &&
                x.CustomerProfileId == profile.Id &&
                x.IsActive,
                cancellationToken);

        if (!hasAccess)
            return Forbid();

        return null;
    }

    private static CustomerRestaurantFloorPlanDto ToCustomerFloorPlanDto(
    RestaurantFloorPlanDto source)
    {
        return new CustomerRestaurantFloorPlanDto
        {
            BusinessId = source.BusinessId,
            RestaurantAreaId = source.RestaurantAreaId,
            AreaName = source.AreaName,
            CanvasWidth = source.CanvasWidth,
            CanvasHeight = source.CanvasHeight,
            BoundaryPointsJson = source.BoundaryPointsJson,
            StatusAtUtc = source.StatusAtUtc,

            Elements = source.Elements
                .Select(x => new CustomerRestaurantLayoutElementDto
                {
                    Id = x.Id,
                    ElementType = x.ElementType,
                    Label = x.Label,
                    X = x.X,
                    Y = x.Y,
                    Width = x.Width,
                    Height = x.Height,
                    RotationDeg = x.RotationDeg,
                    ShapeType = x.ShapeType,
                    PointsJson = x.PointsJson,
                    IsObstacle = x.IsObstacle,
                    DisplayOrder = x.DisplayOrder
                })
                .ToList(),

            Tables = source.Resources
                .Select(ToCustomerTableDto)
                .ToList()
        };
    }

    private static CustomerRestaurantFloorPlanTableDto ToCustomerTableDto(
        RestaurantFloorPlanResourceDto source)
    {
        var nextReservation = source.UpcomingReservations
            .OrderBy(x => x.ReservationAtUtc)
            .FirstOrDefault();

        DateTime? reservedFromUtc = null;
        DateTime? reservedUntilUtc = null;

        if (nextReservation is not null)
        {
            reservedFromUtc = nextReservation.ReservationAtUtc;

            var durationMin = nextReservation.ExpectedDurationMin.GetValueOrDefault(120);

            if (durationMin <= 0)
                durationMin = 120;

            reservedUntilUtc = nextReservation.ReservationAtUtc.AddMinutes(durationMin);
        }

        var customerStatus = GetCustomerTableStatus(source);
        var customerStatusText = GetCustomerTableStatusText(customerStatus);

        return new CustomerRestaurantFloorPlanTableDto
        {
            ResourceId = source.ResourceId,
            Name = source.Name,
            Capacity = source.Capacity,

            Status = customerStatus,
            StatusText = customerStatusText,

            BusyUntilUtc = source.CurrentTableSessionId.HasValue
                ? reservedFromUtc
                : null,

            ReservedFromUtc = source.CurrentTableSessionId.HasValue
                ? null
                : reservedFromUtc,

            ReservedUntilUtc = source.CurrentTableSessionId.HasValue
                ? null
                : reservedUntilUtc,

            LayoutX = source.LayoutX,
            LayoutY = source.LayoutY,
            LayoutWidth = source.LayoutWidth,
            LayoutHeight = source.LayoutHeight,
            LayoutRotationDeg = source.LayoutRotationDeg,
            LayoutShape = source.LayoutShape,
            LayoutPointsJson = source.LayoutPointsJson
        };
    }

    private static int GetCustomerTableStatus(RestaurantFloorPlanResourceDto source)
    {
        if (!source.IsActive)
            return 3;

        if (source.CurrentTableSessionId.HasValue)
            return 1;

        if (source.UpcomingReservations is not null &&
            source.UpcomingReservations.Count > 0)
            return 2;

        return source.StatusText switch
        {
            "Sala zauzeta" => 3,
            "Neaktivno" => 3,
            _ => 0
        };
    }

    private static string GetCustomerTableStatusText(int status)
    {
        return status switch
        {
            0 => "Slobodan",
            1 => "Zauzet",
            2 => "Rezervisan",
            3 => "Nije dostupan",
            _ => "Nepoznat status"
        };
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    private static string GetAreaStatusText(RestaurantAreaVisualStatusDto status)
    {
        return status switch
        {
            RestaurantAreaVisualStatusDto.Available => "Sala slobodna",
            RestaurantAreaVisualStatusDto.Occupied => "Sala zauzeta",
            RestaurantAreaVisualStatusDto.PendingReservation => "Sala čeka potvrdu",
            RestaurantAreaVisualStatusDto.Inactive => "Sala neaktivna",
            _ => "Nepoznat status"
        };
    }

    private static string GetResourceStatusText(RestaurantResourceVisualStatusDto status)
    {
        return status switch
        {
            RestaurantResourceVisualStatusDto.Available => "Slobodno",
            RestaurantResourceVisualStatusDto.Occupied => "Zauzeto",
            RestaurantResourceVisualStatusDto.ReservedLater => "Rezervisano uskoro",
            RestaurantResourceVisualStatusDto.PendingReservation => "Čeka potvrdu",
            RestaurantResourceVisualStatusDto.Inactive => "Neaktivno",
            RestaurantResourceVisualStatusDto.AreaOccupied => "Sala zauzeta",
            _ => "Nepoznat status"
        };
    }

    private static string GetReservationStatusText(RestaurantTableReservationStatus status)
    {
        return status switch
        {
            RestaurantTableReservationStatus.PendingApproval => "Čeka potvrdu",
            RestaurantTableReservationStatus.Confirmed => "Potvrđeno",
            RestaurantTableReservationStatus.Rejected => "Odbijeno",
            RestaurantTableReservationStatus.Cancelled => "Otkazano",
            RestaurantTableReservationStatus.Arrived => "Došli",
            RestaurantTableReservationStatus.NoShow => "Nisu došli",
            _ => "Nepoznat status"
        };
    }

    private static string GetOrderStatusText(RestaurantOrderStatus status)
    {
        return status switch
        {
            RestaurantOrderStatus.Draft => "Nacrt",
            RestaurantOrderStatus.Submitted => "Poslato",
            RestaurantOrderStatus.Preparing => "U pripremi",
            RestaurantOrderStatus.Ready => "Spremno",
            RestaurantOrderStatus.Served => "Posluženo",
            RestaurantOrderStatus.Cancelled => "Otkazano",
            _ => "Nepoznat status"
        };
    }
}