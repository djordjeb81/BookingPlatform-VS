using BookingPlatform.Contracts.Common;
using BookingPlatform.Contracts.Restaurants;
using BookingPlatform.Domain.Resources;
using BookingPlatform.Domain.Restaurants;
using BookingPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BookingPlatform.Api.Services;

namespace BookingPlatform.Api.Controllers;

[ApiController]
[Authorize]
[Produces("application/json")]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
[Route("api/[controller]")]
public sealed class RestaurantTableOperationsController : ApiControllerBase
{
    private const int TableShouldBeFreeAlarmLeadMinutes = 15;

    private readonly ISystemAlarmService _systemAlarmService;

    public RestaurantTableOperationsController(
        BookingDbContext dbContext,
        ISystemAlarmService systemAlarmService)
        : base(dbContext)
    {
        _systemAlarmService = systemAlarmService;
    }

    [HttpGet("active-sessions")]
    public async Task<ActionResult<List<RestaurantTableSessionDto>>> GetActiveSessions(
        [FromQuery] long businessId,
        [FromQuery] long? restaurantAreaId,
        CancellationToken cancellationToken)
    {
        if (businessId <= 0)
            return BadRequest("businessId je obavezan.");

        var accessResult = await EnsureBusinessReadAccessAsync(businessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var query = DbContext.RestaurantTableSessions
            .AsNoTracking()
            .Include(x => x.TableResource)
            .Where(x =>
                x.BusinessId == businessId &&
                x.Status == RestaurantTableSessionStatus.Active &&
                x.ReleasedAtUtc == null);

        if (restaurantAreaId.HasValue)
            query = query.Where(x => x.RestaurantAreaId == restaurantAreaId.Value);

        var items = await query
            .OrderBy(x => x.StartedAtUtc)
            .Select(x => new RestaurantTableSessionDto
            {
                Id = x.Id,
                BusinessId = x.BusinessId,
                RestaurantAreaId = x.RestaurantAreaId,
                TableResourceId = x.TableResourceId,
                TableName = x.TableResource.Name,
                PartySize = x.PartySize,
                CustomerName = x.CustomerName,
                CustomerPhone = x.CustomerPhone,
                Note = x.Note,
                Status = (int)x.Status,
                StatusText = GetStatusText(x.Status),
                StartedAtUtc = x.StartedAtUtc,
                ReleasedAtUtc = x.ReleasedAtUtc
            })
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpGet("{sessionId:long}")]
    public async Task<ActionResult<RestaurantTableSessionDto>> GetById(
        [FromRoute] long sessionId,
        CancellationToken cancellationToken)
    {
        var entity = await DbContext.RestaurantTableSessions
            .AsNoTracking()
            .Include(x => x.TableResource)
            .FirstOrDefaultAsync(x => x.Id == sessionId, cancellationToken);

        if (entity is null)
            return NotFound("Zauzeće stola ne postoji.");

        var accessResult = await EnsureBusinessReadAccessAsync(entity.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        return Ok(ToDto(entity));
    }

    [HttpPost("occupy")]
    public async Task<ActionResult<RestaurantTableSessionDto>> Occupy(
        [FromBody] OccupyRestaurantTableRequest request,
        CancellationToken cancellationToken)
    {
        if (request.BusinessId <= 0)
            return BadRequest("businessId je obavezan.");

        if (request.RestaurantAreaId <= 0)
            return BadRequest("restaurantAreaId je obavezan.");

        if (request.TableResourceId <= 0)
            return BadRequest("tableResourceId je obavezan.");

        var accessResult = await EnsureBusinessWriteAccessAsync(request.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (request.PartySize.HasValue && request.PartySize.Value <= 0)
            return BadRequest("Broj gostiju mora biti veći od 0.");

        var area = await DbContext.RestaurantAreas
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x =>
                    x.Id == request.RestaurantAreaId &&
                    x.BusinessId == request.BusinessId &&
                    x.IsActive,
                cancellationToken);

        if (area is null)
            return BadRequest("Izabrana sala ne postoji ili nije aktivna.");

        var table = await DbContext.Resources
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x =>
                    x.Id == request.TableResourceId &&
                    x.BusinessId == request.BusinessId &&
                    x.RestaurantAreaId == request.RestaurantAreaId &&
                    x.IsActive,
                cancellationToken);

        if (table is null)
            return BadRequest("Izabrani sto ne postoji ili ne pripada ovoj sali.");

        if (table.ResourceType != ResourceType.DiningTable && table.ResourceType != ResourceType.Table)
            return BadRequest("Izabrani resurs nije sto.");

        if (request.PartySize.HasValue &&
            table.Capacity.HasValue &&
            request.PartySize.Value > table.Capacity.Value)
        {
            return BadRequest("Broj gostiju je veći od kapaciteta stola.");
        }

        var alreadyActive = await DbContext.RestaurantTableSessions
            .AsNoTracking()
            .AnyAsync(
                x =>
                    x.BusinessId == request.BusinessId &&
                    x.TableResourceId == request.TableResourceId &&
                    x.Status == RestaurantTableSessionStatus.Active &&
                    x.ReleasedAtUtc == null,
                cancellationToken);

        if (alreadyActive)
            return BadRequest("Sto je već zauzet.");

        var now = DateTime.UtcNow;

        var entity = new RestaurantTableSession
        {
            BusinessId = request.BusinessId,
            RestaurantAreaId = request.RestaurantAreaId,
            TableResourceId = request.TableResourceId,
            PartySize = request.PartySize,
            CustomerName = NormalizeText(request.CustomerName, 200),
            CustomerPhone = NormalizeText(request.CustomerPhone, 50),
            Note = NormalizeText(request.Note, 1000),
            Status = RestaurantTableSessionStatus.Active,
            StartedAtUtc = now,
            ReleasedAtUtc = null,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        DbContext.RestaurantTableSessions.Add(entity);
        await DbContext.SaveChangesAsync(cancellationToken);

        entity = await DbContext.RestaurantTableSessions
            .AsNoTracking()
            .Include(x => x.TableResource)
            .FirstAsync(x => x.Id == entity.Id, cancellationToken);

        await CreateTableShouldBeFreeAlarmForSessionIfNeededAsync(
            entity,
            cancellationToken);

        return Ok(ToDto(entity));
    }

    [HttpPut("{sessionId:long}")]
    public async Task<ActionResult<RestaurantTableSessionDto>> Update(
        [FromRoute] long sessionId,
        [FromBody] UpdateRestaurantTableSessionRequest request,
        CancellationToken cancellationToken)
    {
        var entity = await DbContext.RestaurantTableSessions
            .Include(x => x.TableResource)
            .FirstOrDefaultAsync(x => x.Id == sessionId, cancellationToken);

        if (entity is null)
            return NotFound("Zauzeće stola ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(entity.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (entity.Status != RestaurantTableSessionStatus.Active || entity.ReleasedAtUtc.HasValue)
            return BadRequest("Moguće je menjati samo aktivno zauzeće stola.");

        if (request.PartySize.HasValue && request.PartySize.Value <= 0)
            return BadRequest("Broj gostiju mora biti veći od 0.");

        if (request.PartySize.HasValue &&
            entity.TableResource.Capacity.HasValue &&
            request.PartySize.Value > entity.TableResource.Capacity.Value)
        {
            return BadRequest("Broj gostiju je veći od kapaciteta stola.");
        }

        entity.PartySize = request.PartySize;
        entity.CustomerName = NormalizeText(request.CustomerName, 200);
        entity.CustomerPhone = NormalizeText(request.CustomerPhone, 50);
        entity.Note = NormalizeText(request.Note, 1000);
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await DbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(entity));
    }

    [HttpPost("release")]
    public async Task<ActionResult<RestaurantTableSessionDto>> Release(
        [FromBody] ReleaseRestaurantTableRequest request,
        CancellationToken cancellationToken)
    {
        if (request.SessionId <= 0)
            return BadRequest("sessionId je obavezan.");

        var entity = await DbContext.RestaurantTableSessions
            .Include(x => x.TableResource)
            .FirstOrDefaultAsync(x => x.Id == request.SessionId, cancellationToken);

        if (entity is null)
            return NotFound("Zauzeće stola ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(entity.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (entity.Status != RestaurantTableSessionStatus.Active || entity.ReleasedAtUtc.HasValue)
            return BadRequest("Sto je već oslobođen.");

        var activeOrders = await DbContext.RestaurantOrders
            .AsNoTracking()
            .Where(x =>
                x.TableSessionId == entity.Id &&
                x.Status != RestaurantOrderStatus.Cancelled)
            .ToListAsync(cancellationToken);

        var hasUnfinishedOrders = activeOrders.Any(x => x.Status != RestaurantOrderStatus.Served);

        if (hasUnfinishedOrders)
        {
            return BadRequest("Sto ne može da se oslobodi dok ima aktivnih narudžbina. Prvo završite ili otkažite narudžbine.");
        }


        var now = DateTime.UtcNow;

        entity.Status = RestaurantTableSessionStatus.Released;
        entity.ReleasedAtUtc = now;

        AppendNote(entity, request.Note);

        entity.UpdatedAtUtc = now;

        await DbContext.SaveChangesAsync(cancellationToken);

        await CancelTableShouldBeFreeAlarmsForSessionAsync(
            entity,
            cancellationToken);

        return Ok(ToDto(entity));
    }

    [HttpPost("move")]
    public async Task<ActionResult<RestaurantTableSessionDto>> Move(
    [FromBody] MoveRestaurantTableSessionRequest request,
    CancellationToken cancellationToken)
    {
        if (request.SessionId <= 0)
            return BadRequest("sessionId je obavezan.");

        if (request.NewTableResourceId <= 0)
            return BadRequest("newTableResourceId je obavezan.");

        var entity = await DbContext.RestaurantTableSessions
            .Include(x => x.TableResource)
            .FirstOrDefaultAsync(x => x.Id == request.SessionId, cancellationToken);

        if (entity is null)
            return NotFound("Zauzeće stola ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(entity.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (entity.Status != RestaurantTableSessionStatus.Active || entity.ReleasedAtUtc.HasValue)
            return BadRequest("Moguće je premestiti samo aktivno zauzeće stola.");

        if (entity.TableResourceId == request.NewTableResourceId)
            return BadRequest("Gosti su već za izabranim stolom.");

        var newTable = await DbContext.Resources
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x =>
                    x.Id == request.NewTableResourceId &&
                    x.BusinessId == entity.BusinessId &&
                    x.RestaurantAreaId == entity.RestaurantAreaId &&
                    x.IsActive,
                cancellationToken);

        if (newTable is null)
            return BadRequest("Novi sto ne postoji, nije aktivan ili ne pripada istoj sali.");

        if (newTable.ResourceType != ResourceType.DiningTable && newTable.ResourceType != ResourceType.Table)
            return BadRequest("Izabrani resurs nije sto.");

        if (entity.PartySize.HasValue &&
            newTable.Capacity.HasValue &&
            entity.PartySize.Value > newTable.Capacity.Value)
        {
            return BadRequest("Broj gostiju je veći od kapaciteta novog stola.");
        }

        var newTableAlreadyActive = await DbContext.RestaurantTableSessions
            .AsNoTracking()
            .AnyAsync(
                x =>
                    x.BusinessId == entity.BusinessId &&
                    x.TableResourceId == request.NewTableResourceId &&
                    x.Status == RestaurantTableSessionStatus.Active &&
                    x.ReleasedAtUtc == null,
                cancellationToken);

        if (newTableAlreadyActive)
            return BadRequest("Novi sto je već zauzet.");

        var oldTableName = entity.TableResource.Name;
        var newTableName = newTable.Name;

        entity.TableResourceId = request.NewTableResourceId;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        var note = string.IsNullOrWhiteSpace(request.Note)
            ? $"Gosti premešteni sa stola \"{oldTableName}\" na sto \"{newTableName}\"."
            : request.Note;

        AppendNote(entity, note);

        await DbContext.SaveChangesAsync(cancellationToken);

        entity = await DbContext.RestaurantTableSessions
            .AsNoTracking()
            .Include(x => x.TableResource)
            .FirstAsync(x => x.Id == entity.Id, cancellationToken);

        await CreateTableShouldBeFreeAlarmForSessionIfNeededAsync(
            entity,
            cancellationToken);

        return Ok(ToDto(entity));
    }

    [HttpPost("cancel")]
    public async Task<ActionResult<RestaurantTableSessionDto>> Cancel(
        [FromBody] CancelRestaurantTableSessionRequest request,
        CancellationToken cancellationToken)
    {
        if (request.SessionId <= 0)
            return BadRequest("sessionId je obavezan.");

        var entity = await DbContext.RestaurantTableSessions
            .Include(x => x.TableResource)
            .FirstOrDefaultAsync(x => x.Id == request.SessionId, cancellationToken);

        if (entity is null)
            return NotFound("Zauzeće stola ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(entity.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (entity.Status != RestaurantTableSessionStatus.Active || entity.ReleasedAtUtc.HasValue)
            return BadRequest("Moguće je otkazati samo aktivno zauzeće stola.");

        var hasOrders = await DbContext.RestaurantOrders
            .AsNoTracking()
            .AnyAsync(x => x.TableSessionId == entity.Id, cancellationToken);

        if (hasOrders)
        {
            return BadRequest("Zauzeće stola ne može da se otkaže jer ima narudžbine. Umesto otkazivanja završite narudžbine, naplatite račun i oslobodite sto.");
        }

        var hasPayments = await DbContext.RestaurantPayments
            .AsNoTracking()
            .AnyAsync(x => x.TableSessionId == entity.Id, cancellationToken);

        if (hasPayments)
        {
            return BadRequest("Zauzeće stola ne može da se otkaže jer ima evidentirane uplate.");
        }

        var now = DateTime.UtcNow;

        entity.Status = RestaurantTableSessionStatus.Cancelled;
        entity.ReleasedAtUtc = now;

        AppendNote(entity, request.Note);

        entity.UpdatedAtUtc = now;

        await DbContext.SaveChangesAsync(cancellationToken);

        await CancelTableShouldBeFreeAlarmsForSessionAsync(
            entity,
            cancellationToken);

        return Ok(ToDto(entity));
    }

    [HttpPost("close-old-active-sessions")]
    public async Task<ActionResult<CloseOldRestaurantTableSessionsResponse>> CloseOldActiveSessions(
     [FromBody] CloseOldRestaurantTableSessionsRequest request,
     CancellationToken cancellationToken)
    {
        if (request.BusinessId <= 0)
            return BadRequest("businessId je obavezan.");

        var accessResult = await EnsureBusinessWriteAccessAsync(request.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (request.RestaurantAreaId.HasValue)
        {
            var areaExists = await DbContext.RestaurantAreas
                .AsNoTracking()
                .AnyAsync(x =>
                    x.Id == request.RestaurantAreaId.Value &&
                    x.BusinessId == request.BusinessId,
                    cancellationToken);

            if (!areaExists)
                return BadRequest("Izabrana sala ne postoji ili ne pripada ovoj radnji.");
        }

        var nowUtc = DateTime.UtcNow;
        var todayUtc = nowUtc.Date;

        var query = DbContext.RestaurantTableSessions
            .Include(x => x.TableResource)
            .Where(x =>
                x.BusinessId == request.BusinessId &&
                x.Status == RestaurantTableSessionStatus.Active &&
                x.ReleasedAtUtc == null &&
                x.StartedAtUtc < todayUtc);

        if (request.RestaurantAreaId.HasValue)
            query = query.Where(x => x.RestaurantAreaId == request.RestaurantAreaId.Value);

        var sessions = await query
            .OrderBy(x => x.StartedAtUtc)
            .ToListAsync(cancellationToken);

        if (sessions.Count == 0)
        {
            return Ok(new CloseOldRestaurantTableSessionsResponse
            {
                BusinessId = request.BusinessId,
                RestaurantAreaId = request.RestaurantAreaId,
                ClosedCount = 0,
                SkippedCount = 0,
                ClosedAtUtc = nowUtc,
                Message = "Nema starih aktivnih zauzeća za zatvaranje."
            });
        }

        var sessionIds = sessions
            .Select(x => x.Id)
            .ToList();

        var orders = await DbContext.RestaurantOrders
            .AsNoTracking()
            .Where(x => x.TableSessionId.HasValue && sessionIds.Contains(x.TableSessionId.Value))
            .ToListAsync(cancellationToken);

        var ordersBySessionId = orders
            .Where(x => x.TableSessionId.HasValue)
            .GroupBy(x => x.TableSessionId!.Value)
            .ToDictionary(x => x.Key, x => x.ToList());

        var skipped = new List<CloseOldRestaurantTableSessionSkippedDto>();
        var closedCount = 0;

        foreach (var session in sessions)
        {
            ordersBySessionId.TryGetValue(session.Id, out var sessionOrders);
            sessionOrders ??= new List<RestaurantOrder>();

            var hasUnfinishedOrders = sessionOrders.Any(x =>
                x.Status != RestaurantOrderStatus.Served &&
                x.Status != RestaurantOrderStatus.Cancelled);

            if (hasUnfinishedOrders)
            {
                skipped.Add(new CloseOldRestaurantTableSessionSkippedDto
                {
                    SessionId = session.Id,
                    TableResourceId = session.TableResourceId,
                    TableName = session.TableResource?.Name,
                    Reason = "Ima aktivne narudžbine."
                });

                continue;
            }

            session.Status = RestaurantTableSessionStatus.Released;
            session.ReleasedAtUtc = nowUtc;
            session.UpdatedAtUtc = nowUtc;

            AppendNote(
                session,
                string.IsNullOrWhiteSpace(request.Note)
                    ? "Automatski zatvoreno jer je zauzeće ostalo aktivno od prethodnog dana."
                    : request.Note);

            closedCount++;
        }

        await DbContext.SaveChangesAsync(cancellationToken);

        return Ok(new CloseOldRestaurantTableSessionsResponse
        {
            BusinessId = request.BusinessId,
            RestaurantAreaId = request.RestaurantAreaId,
            ClosedCount = closedCount,
            SkippedCount = skipped.Count,
            ClosedAtUtc = nowUtc,
            SkippedSessions = skipped,
            Message = closedCount == 0 && skipped.Count == 0
                ? "Nema starih aktivnih zauzeća za zatvaranje."
                : $"Zatvoreno: {closedCount}. Preskočeno: {skipped.Count}."
        });
    }


    [HttpDelete("{sessionId:long}")]
    public async Task<ActionResult> Delete(
        [FromRoute] long sessionId,
        CancellationToken cancellationToken)
    {
        var entity = await DbContext.RestaurantTableSessions
            .FirstOrDefaultAsync(x => x.Id == sessionId, cancellationToken);

        if (entity is null)
            return NotFound("Zauzeće stola ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(entity.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (entity.Status != RestaurantTableSessionStatus.Active || entity.ReleasedAtUtc.HasValue)
        {
            return BadRequest("Završeno ili otkazano zauzeće se ne briše. Ono ostaje kao istorija rada.");
        }

        var hasOrders = await DbContext.RestaurantOrders
            .AsNoTracking()
            .AnyAsync(x => x.TableSessionId == entity.Id, cancellationToken);

        if (hasOrders)
        {
            return BadRequest("Zauzeće stola ne može da se obriše jer ima narudžbine. Možete ga osloboditi ili otkazati.");
        }

        await CancelTableShouldBeFreeAlarmsForSessionAsync(
            entity,
            cancellationToken);

        DbContext.RestaurantTableSessions.Remove(entity);
        await DbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private async Task CreateTableShouldBeFreeAlarmForSessionIfNeededAsync(
    RestaurantTableSession session,
    CancellationToken cancellationToken)
    {
        if (session.Status != RestaurantTableSessionStatus.Active ||
            session.ReleasedAtUtc.HasValue)
        {
            return;
        }

        var nowUtc = DateTime.UtcNow;

        var nextReservation = await DbContext.RestaurantTableReservations
            .AsNoTracking()
            .Where(x =>
                x.BusinessId == session.BusinessId &&
                x.RestaurantAreaId == session.RestaurantAreaId &&
                x.TableResourceId == session.TableResourceId &&
                x.Status == RestaurantTableReservationStatus.Confirmed &&
                x.ReservationAtUtc > nowUtc)
            .OrderBy(x => x.ReservationAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (nextReservation is null)
            return;

        var triggerAtUtc = nextReservation.ReservationAtUtc
            .AddMinutes(-TableShouldBeFreeAlarmLeadMinutes);

        if (triggerAtUtc < nowUtc)
            triggerAtUtc = nowUtc;

        var targetRestaurantOperationUnitId = await GetDefaultRestaurantOperationUnitIdAsync(
            session.BusinessId,
            cancellationToken);

        var tableName = session.TableResource?.Name;

        if (string.IsNullOrWhiteSpace(tableName))
            tableName = $"Sto #{session.TableResourceId}";

        await _systemAlarmService.CreateRestaurantTableShouldBeFreeAlarmAsync(
            session.BusinessId,
            nextReservation.Id,
            session.Id,
            session.TableResourceId,
            tableName,
            nextReservation.ReservationAtUtc,
            nextReservation.PartySize,
            triggerAtUtc,
            targetRestaurantOperationUnitId,
            cancellationToken);
    }

    private async Task CancelTableShouldBeFreeAlarmsForSessionAsync(
    RestaurantTableSession session,
    CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;

        var relatedReservations = await DbContext.RestaurantTableReservations
            .AsNoTracking()
            .Where(x =>
                x.BusinessId == session.BusinessId &&
                x.RestaurantAreaId == session.RestaurantAreaId &&
                x.TableResourceId == session.TableResourceId &&
                x.Status == RestaurantTableReservationStatus.Confirmed &&
                x.ReservationAtUtc > nowUtc)
            .OrderBy(x => x.ReservationAtUtc)
            .Take(10)
            .ToListAsync(cancellationToken);

        foreach (var reservation in relatedReservations)
        {
            await _systemAlarmService.CancelRestaurantTableShouldBeFreeAlarmForReservationAsync(
                reservation.BusinessId,
                reservation.Id,
                cancellationToken);
        }
    }

    private async Task<long?> GetDefaultRestaurantOperationUnitIdAsync(
        long businessId,
        CancellationToken cancellationToken)
    {
        return await DbContext.RestaurantOperationUnits
            .AsNoTracking()
            .Where(x =>
                x.BusinessId == businessId &&
                x.IsActive &&
                x.UnitType == RestaurantOperationUnitType.DiningRoom)
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Id)
            .Select(x => (long?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static RestaurantTableSessionDto ToDto(RestaurantTableSession entity)
    {
        return new RestaurantTableSessionDto
        {
            Id = entity.Id,
            BusinessId = entity.BusinessId,
            RestaurantAreaId = entity.RestaurantAreaId,
            TableResourceId = entity.TableResourceId,
            TableName = entity.TableResource.Name,
            PartySize = entity.PartySize,
            CustomerName = entity.CustomerName,
            CustomerPhone = entity.CustomerPhone,
            Note = entity.Note,
            Status = (int)entity.Status,
            StatusText = GetStatusText(entity.Status),
            StartedAtUtc = entity.StartedAtUtc,
            ReleasedAtUtc = entity.ReleasedAtUtc
        };
    }

    private static string GetStatusText(RestaurantTableSessionStatus status)
    {
        return status switch
        {
            RestaurantTableSessionStatus.Active => "Zauzeto",
            RestaurantTableSessionStatus.Released => "Oslobođeno",
            RestaurantTableSessionStatus.Cancelled => "Otkazano",
            _ => "Nepoznat status"
        };
    }

    private static void AppendNote(RestaurantTableSession entity, string? note)
    {
        var normalizedNote = NormalizeText(note, 1000);

        if (string.IsNullOrWhiteSpace(normalizedNote))
            return;

        entity.Note = string.IsNullOrWhiteSpace(entity.Note)
            ? normalizedNote
            : $"{entity.Note}\n{normalizedNote}";
    }

    private static string? NormalizeText(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();

        return trimmed.Length <= maxLength
            ? trimmed
            : trimmed[..maxLength];
    }
}