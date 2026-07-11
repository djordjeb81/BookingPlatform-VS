using BookingPlatform.Contracts.Common;
using BookingPlatform.Contracts.Restaurants;
using BookingPlatform.Domain.Resources;
using BookingPlatform.Domain.Restaurants;
using BookingPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BookingPlatform.Api.Services;
using BookingPlatform.Domain.SystemAlarms;

namespace BookingPlatform.Api.Controllers;

[ApiController]
[Authorize]
[Produces("application/json")]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
[Route("api/[controller]")]
public sealed class RestaurantTableReservationsController : ApiControllerBase
{
    private const int TableShouldBeFreeAlarmLeadMinutes = 15;

    private readonly ISystemAlarmService _systemAlarmService;

    private readonly IChatSystemMessageService _chatSystemMessageService;

    public RestaurantTableReservationsController(
        BookingDbContext dbContext,
        ISystemAlarmService systemAlarmService,
        IChatSystemMessageService chatSystemMessageService)
        : base(dbContext)
    {
        _systemAlarmService = systemAlarmService;
        _chatSystemMessageService = chatSystemMessageService;
    }

    [HttpGet]
    public async Task<ActionResult<List<RestaurantTableReservationDto>>> GetAll(
        [FromQuery] long businessId,
        [FromQuery] long? restaurantAreaId,
        [FromQuery] long? tableResourceId,
        [FromQuery] int? status,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        CancellationToken cancellationToken)
    {
        if (businessId <= 0)
            return BadRequest("businessId je obavezan.");

        var accessResult = await EnsureBusinessReadAccessAsync(businessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var query = DbContext.RestaurantTableReservations
            .AsNoTracking()
            .Include(x => x.TableResource)
            .Where(x => x.BusinessId == businessId);

        if (restaurantAreaId.HasValue)
            query = query.Where(x => x.RestaurantAreaId == restaurantAreaId.Value);

        if (tableResourceId.HasValue)
            query = query.Where(x => x.TableResourceId == tableResourceId.Value);

        if (status.HasValue)
        {
            if (!Enum.IsDefined(typeof(RestaurantTableReservationStatus), status.Value))
                return BadRequest("Nepoznat status rezervacije.");

            query = query.Where(x => x.Status == (RestaurantTableReservationStatus)status.Value);
        }

        if (fromUtc.HasValue)
        {
            var from = EnsureUtc(fromUtc.Value);
            query = query.Where(x => x.ReservationAtUtc >= from);
        }

        if (toUtc.HasValue)
        {
            var to = EnsureUtc(toUtc.Value);
            query = query.Where(x => x.ReservationAtUtc < to);
        }

        var items = await query
            .OrderBy(x => x.ReservationAtUtc)
            .Take(300)
            .ToListAsync(cancellationToken);

        return Ok(items.Select(ToDto).ToList());
    }

    [HttpGet("available-tables")]
    public async Task<ActionResult<List<AvailableRestaurantTableDto>>> GetAvailableTables(
    [FromQuery] long businessId,
    [FromQuery] long restaurantAreaId,
    [FromQuery] int partySize,
    [FromQuery] DateTime reservationAtUtc,
    [FromQuery] int? expectedDurationMin,
    CancellationToken cancellationToken)
    {
        if (businessId <= 0)
            return BadRequest("businessId je obavezan.");

        if (restaurantAreaId <= 0)
            return BadRequest("restaurantAreaId je obavezan.");

        if (partySize <= 0)
            return BadRequest("Broj gostiju mora biti veći od 0.");

        var accessResult = await EnsureBusinessReadAccessAsync(businessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var areaExists = await DbContext.RestaurantAreas
            .AsNoTracking()
            .AnyAsync(x =>
                x.Id == restaurantAreaId &&
                x.BusinessId == businessId &&
                x.IsActive,
                cancellationToken);

        if (!areaExists)
            return BadRequest("Izabrana sala ne postoji ili nije aktivna.");

        var reservationStartUtc = EnsureUtc(reservationAtUtc);

        if (reservationStartUtc < DateTime.UtcNow.AddMinutes(-5))
            return BadRequest("Vreme rezervacije ne može biti u prošlosti.");

        var durationMin = expectedDurationMin.GetValueOrDefault(120);

        if (durationMin <= 0)
            durationMin = 120;

        var reservationEndUtc = reservationStartUtc.AddMinutes(durationMin);

        var areaReservations = await DbContext.RestaurantAreaReservations
    .AsNoTracking()
    .Where(x =>
        x.BusinessId == businessId &&
        x.RestaurantAreaId == restaurantAreaId &&
        x.Status != RestaurantAreaReservationStatus.Rejected &&
        x.Status != RestaurantAreaReservationStatus.Cancelled &&
        x.Status != RestaurantAreaReservationStatus.NoShow &&
        x.Status != RestaurantAreaReservationStatus.Completed)
    .Select(x => new
    {
        x.ReservationAtUtc,
        x.ExpectedDurationMin
    })
    .ToListAsync(cancellationToken);

        foreach (var areaReservation in areaReservations)
        {
            var existingStartUtc = EnsureUtc(areaReservation.ReservationAtUtc);
            var existingDurationMin = areaReservation.ExpectedDurationMin.GetValueOrDefault(240);

            if (existingDurationMin <= 0)
                existingDurationMin = 240;

            var existingEndUtc = existingStartUtc.AddMinutes(existingDurationMin);

            var overlaps = existingStartUtc < reservationEndUtc &&
                           reservationStartUtc < existingEndUtc;

            if (overlaps)
                return Ok(new List<AvailableRestaurantTableDto>());
        }

        var tables = await DbContext.Resources
            .AsNoTracking()
            .Where(x =>
                x.BusinessId == businessId &&
                x.RestaurantAreaId == restaurantAreaId &&
                x.IsActive &&
                (x.ResourceType == ResourceType.Table || x.ResourceType == ResourceType.DiningTable) &&
                (!x.Capacity.HasValue || x.Capacity.Value >= partySize))
            .OrderBy(x => x.Capacity ?? int.MaxValue)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        if (tables.Count == 0)
            return Ok(new List<AvailableRestaurantTableDto>());

        var tableIds = tables
            .Select(x => x.Id)
            .ToList();

        var conflictingReservationTableIds = await DbContext.RestaurantTableReservations
            .AsNoTracking()
            .Where(x =>
                x.BusinessId == businessId &&
                x.RestaurantAreaId == restaurantAreaId &&
                x.TableResourceId.HasValue &&
                tableIds.Contains(x.TableResourceId.Value) &&
                x.Status != RestaurantTableReservationStatus.Rejected &&
                x.Status != RestaurantTableReservationStatus.Cancelled &&
                x.Status != RestaurantTableReservationStatus.NoShow &&
                x.Status != RestaurantTableReservationStatus.Arrived)
                    .Select(x => new
            {
                TableResourceId = x.TableResourceId!.Value,
                x.ReservationAtUtc,
                x.ExpectedDurationMin
            })
            .ToListAsync(cancellationToken);

        var unavailableTableIds = new HashSet<long>();

        foreach (var existing in conflictingReservationTableIds)
        {
            var existingStartUtc = EnsureUtc(existing.ReservationAtUtc);
            var existingDurationMin = existing.ExpectedDurationMin.GetValueOrDefault(120);

            if (existingDurationMin <= 0)
                existingDurationMin = 120;

            var existingEndUtc = existingStartUtc.AddMinutes(existingDurationMin);

            var overlaps = existingStartUtc < reservationEndUtc && reservationStartUtc < existingEndUtc;

            if (overlaps)
                unavailableTableIds.Add(existing.TableResourceId);
        }

        var activeSessionTableIds = await DbContext.RestaurantTableSessions
            .AsNoTracking()
            .Where(x =>
                x.BusinessId == businessId &&
                x.RestaurantAreaId == restaurantAreaId &&
                tableIds.Contains(x.TableResourceId) &&
                x.Status == RestaurantTableSessionStatus.Active &&
                x.ReleasedAtUtc == null)
            .Select(x => x.TableResourceId)
            .ToListAsync(cancellationToken);

        foreach (var tableId in activeSessionTableIds)
            unavailableTableIds.Add(tableId);

        var availableTables = tables
            .Where(x => !unavailableTableIds.Contains(x.Id))
            .Select(x =>
            {
                var capacity = x.Capacity ?? partySize;
                var capacityDifference = capacity - partySize;

                return new AvailableRestaurantTableDto
                {
                    TableResourceId = x.Id,
                    TableName = x.Name,
                    Capacity = x.Capacity,
                    CapacityDifference = capacityDifference,
                    LayoutX = x.LayoutX,
                    LayoutY = x.LayoutY,
                    LayoutWidth = x.LayoutWidth,
                    LayoutHeight = x.LayoutHeight,
                    LayoutRotationDeg = x.LayoutRotationDeg,
                    LayoutShape = (int)x.LayoutShape,
                    LayoutPointsJson = x.LayoutPointsJson
                };
            })
            .OrderBy(x => x.CapacityDifference)
            .ThenBy(x => x.TableName)
            .ToList();

        var bestCapacityDifference = availableTables.Count == 0
            ? (int?)null
            : availableTables.Min(x => x.CapacityDifference);

        foreach (var table in availableTables)
        {
            table.IsBestFit = bestCapacityDifference.HasValue &&
                              table.CapacityDifference == bestCapacityDifference.Value;
        }

        return Ok(availableTables);
    }

    [HttpGet("{reservationId:long}")]
    public async Task<ActionResult<RestaurantTableReservationDto>> GetById(
        [FromRoute] long reservationId,
        CancellationToken cancellationToken)
    {
        var entity = await DbContext.RestaurantTableReservations
            .AsNoTracking()
            .Include(x => x.TableResource)
            .FirstOrDefaultAsync(x => x.Id == reservationId, cancellationToken);

        if (entity is null)
            return NotFound("Rezervacija ne postoji.");

        var accessResult = await EnsureBusinessReadAccessAsync(entity.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        return Ok(ToDto(entity));
    }

    [HttpPost]
    public async Task<ActionResult<RestaurantTableReservationDto>> Create(
        [FromBody] CreateRestaurantTableReservationRequest request,
        CancellationToken cancellationToken)
    {
        if (request.BusinessId <= 0)
            return BadRequest("businessId je obavezan.");

        var accessResult = await EnsureBusinessWriteAccessAsync(request.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var validationResult = await ValidateReservationRequestAsync(
            request.BusinessId,
            request.RestaurantAreaId,
            request.TableResourceId,
            request.PartySize,
            request.CustomerName,
            request.CustomerPhone,
            request.ReservationAtUtc,
            request.ExpectedDurationMin,
            cancellationToken);

        if (validationResult is not null)
            return validationResult;

        var areaConflictResult = await ValidateAreaReservationConflictForTableReservationAsync(
            request.BusinessId,
            request.RestaurantAreaId,
            EnsureUtc(request.ReservationAtUtc),
            request.ExpectedDurationMin,
            cancellationToken);

        if (areaConflictResult is not null)
            return areaConflictResult;

        var conflictResult = await ValidateReservationConflictAsync(
            request.BusinessId,
            request.TableResourceId,
            EnsureUtc(request.ReservationAtUtc),
            request.ExpectedDurationMin,
            null,
            cancellationToken);

        if (conflictResult is not null)
            return conflictResult;

        var now = DateTime.UtcNow;

        var entity = new RestaurantTableReservation
        {
            BusinessId = request.BusinessId,
            RestaurantAreaId = request.RestaurantAreaId,
            TableResourceId = request.TableResourceId,
            PartySize = request.PartySize,
            CustomerName = NormalizeRequiredText(request.CustomerName),
            CustomerPhone = NormalizeRequiredText(request.CustomerPhone),
            CustomerEmail = NormalizeText(request.CustomerEmail, 200),
            ReservationAtUtc = EnsureUtc(request.ReservationAtUtc),
            ExpectedDurationMin = request.ExpectedDurationMin,
            Status = RestaurantTableReservationStatus.PendingApproval,
            Note = NormalizeText(request.Note, 1000),
            InternalNote = NormalizeText(request.InternalNote, 1000),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        DbContext.RestaurantTableReservations.Add(entity);
        await DbContext.SaveChangesAsync(cancellationToken);

        var dtoEntity = await DbContext.RestaurantTableReservations
            .AsNoTracking()
            .Include(x => x.TableResource)
            .FirstAsync(x => x.Id == entity.Id, cancellationToken);

        return Ok(ToDto(dtoEntity));
    }

    [HttpPut("{reservationId:long}")]
    public async Task<ActionResult<RestaurantTableReservationDto>> Update(
        [FromRoute] long reservationId,
        [FromBody] UpdateRestaurantTableReservationRequest request,
        CancellationToken cancellationToken)
    {
        var entity = await DbContext.RestaurantTableReservations
            .Include(x => x.TableResource)
            .FirstOrDefaultAsync(x => x.Id == reservationId, cancellationToken);

        if (entity is null)
            return NotFound("Rezervacija ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(entity.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (entity.Status is RestaurantTableReservationStatus.Arrived
            or RestaurantTableReservationStatus.Cancelled
            or RestaurantTableReservationStatus.Rejected
            or RestaurantTableReservationStatus.NoShow)
        {
            return BadRequest("Ova rezervacija više ne može da se menja.");
        }

        var validationResult = await ValidateReservationRequestAsync(
            entity.BusinessId,
            request.RestaurantAreaId,
            request.TableResourceId,
            request.PartySize,
            request.CustomerName,
            request.CustomerPhone,
            request.ReservationAtUtc,
            request.ExpectedDurationMin,
            cancellationToken);

        if (validationResult is not null)
            return validationResult;

        var areaConflictResult = await ValidateAreaReservationConflictForTableReservationAsync(
            entity.BusinessId,
            request.RestaurantAreaId,
            EnsureUtc(request.ReservationAtUtc),
            request.ExpectedDurationMin,
            cancellationToken);

        if (areaConflictResult is not null)
            return areaConflictResult;

        var conflictResult = await ValidateReservationConflictAsync(
            entity.BusinessId,
            request.TableResourceId,
            EnsureUtc(request.ReservationAtUtc),
            request.ExpectedDurationMin,
            entity.Id,
            cancellationToken);

        if (conflictResult is not null)
            return conflictResult;

        entity.RestaurantAreaId = request.RestaurantAreaId;
        entity.TableResourceId = request.TableResourceId;
        entity.PartySize = request.PartySize;
        entity.CustomerName = NormalizeRequiredText(request.CustomerName);
        entity.CustomerPhone = NormalizeRequiredText(request.CustomerPhone);
        entity.CustomerEmail = NormalizeText(request.CustomerEmail, 200);
        entity.ReservationAtUtc = EnsureUtc(request.ReservationAtUtc);
        entity.ExpectedDurationMin = request.ExpectedDurationMin;
        entity.Note = NormalizeText(request.Note, 1000);
        entity.InternalNote = NormalizeText(request.InternalNote, 1000);
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await DbContext.SaveChangesAsync(cancellationToken);

        await _systemAlarmService.CancelRestaurantTableShouldBeFreeAlarmForReservationAsync(
            entity.BusinessId,
            entity.Id,
            cancellationToken);

        await CreateTableShouldBeFreeAlarmIfNeededAsync(entity, cancellationToken);

        var dtoEntity = await DbContext.RestaurantTableReservations
            .AsNoTracking()
            .Include(x => x.TableResource)
            .FirstAsync(x => x.Id == entity.Id, cancellationToken);

        return Ok(ToDto(dtoEntity));
    }

    [HttpPost("{reservationId:long}/assign-table")]
    public async Task<ActionResult<RestaurantTableReservationDto>> AssignTable(
    [FromRoute] long reservationId,
    [FromBody] AssignRestaurantTableReservationTableRequest request,
    CancellationToken cancellationToken)
    {
        if (request.TableResourceId <= 0)
            return BadRequest("tableResourceId je obavezan.");

        var entity = await LoadReservationForWriteAsync(reservationId, cancellationToken);

        if (entity is null)
            return NotFound("Rezervacija ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(entity.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (entity.Status is RestaurantTableReservationStatus.Arrived
            or RestaurantTableReservationStatus.Cancelled
            or RestaurantTableReservationStatus.Rejected
            or RestaurantTableReservationStatus.NoShow)
        {
            return BadRequest("Ovoj rezervaciji više ne može da se dodeli sto.");
        }

        var tableValidation = await ValidateTableAsync(
       entity.BusinessId,
       entity.RestaurantAreaId,
       request.TableResourceId,
       entity.PartySize,
       cancellationToken);

        if (tableValidation is not null)
            return tableValidation;

        var areaConflictResult = await ValidateAreaReservationConflictForTableReservationAsync(
            entity.BusinessId,
            entity.RestaurantAreaId,
            entity.ReservationAtUtc,
            entity.ExpectedDurationMin,
            cancellationToken);

        if (areaConflictResult is not null)
            return areaConflictResult;

        var conflictResult = await ValidateReservationConflictAsync(
            entity.BusinessId,
            request.TableResourceId,
            entity.ReservationAtUtc,
            entity.ExpectedDurationMin,
            entity.Id,
            cancellationToken);

        if (conflictResult is not null)
            return conflictResult;

        entity.TableResourceId = request.TableResourceId;
        entity.InternalNote = AppendText(entity.InternalNote, request.InternalNote, 1000);
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await DbContext.SaveChangesAsync(cancellationToken);

        await _systemAlarmService.CancelRestaurantTableShouldBeFreeAlarmForReservationAsync(
            entity.BusinessId,
            entity.Id,
            cancellationToken);

        await CreateTableShouldBeFreeAlarmIfNeededAsync(entity, cancellationToken);

        var dtoEntity = await DbContext.RestaurantTableReservations
            .AsNoTracking()
            .Include(x => x.TableResource)
            .FirstAsync(x => x.Id == entity.Id, cancellationToken);

        return Ok(ToDto(dtoEntity));
    }

    [HttpDelete("{reservationId:long}")]
    public async Task<ActionResult> Delete(
        [FromRoute] long reservationId,
        CancellationToken cancellationToken)
    {
        var entity = await DbContext.RestaurantTableReservations
            .FirstOrDefaultAsync(x => x.Id == reservationId, cancellationToken);

        if (entity is null)
            return NotFound("Rezervacija ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(entity.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (entity.Status is RestaurantTableReservationStatus.Arrived)
            return BadRequest("Rezervacija koja je već pretvorena u zauzeće stola ne može da se obriše.");

        if (entity.CreatedTableSessionId.HasValue)
            return BadRequest("Rezervacija ima povezano zauzeće stola i ne može da se obriše.");

        DbContext.RestaurantTableReservations.Remove(entity);
        await DbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpPost("{reservationId:long}/confirm")]
    public async Task<ActionResult<RestaurantTableReservationDto>> Confirm(
     [FromRoute] long reservationId,
     CancellationToken cancellationToken)
    {
        var entity = await LoadReservationForWriteAsync(reservationId, cancellationToken);

        if (entity is null)
            return NotFound("Rezervacija ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(entity.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (entity.Status != RestaurantTableReservationStatus.PendingApproval)
            return BadRequest("Samo rezervacija koja čeka potvrdu može da se potvrdi.");

        var areaConflictResult = await ValidateAreaReservationConflictForTableReservationAsync(
            entity.BusinessId,
            entity.RestaurantAreaId,
            entity.ReservationAtUtc,
            entity.ExpectedDurationMin,
            cancellationToken);

        if (areaConflictResult is not null)
            return areaConflictResult;

        var conflictResult = await ValidateReservationConflictAsync(
            entity.BusinessId,
            entity.TableResourceId,
            entity.ReservationAtUtc,
            entity.ExpectedDurationMin,
            entity.Id,
            cancellationToken);

        if (conflictResult is not null)
            return conflictResult;

        var now = DateTime.UtcNow;

        entity.Status = RestaurantTableReservationStatus.Confirmed;
        entity.RespondedAtUtc = now;
        entity.UpdatedAtUtc = now;

        await DbContext.SaveChangesAsync(cancellationToken);

        await CreateTableShouldBeFreeAlarmIfNeededAsync(entity, cancellationToken);

        if (entity.BusinessCustomerId.HasValue ||
    entity.CustomerProfileId.HasValue ||
    entity.AppUserId.HasValue)
        {
            await _chatSystemMessageService.SendRestaurantTableReservationApprovedOrderPromptToCustomerAsync(
                entity,
                cancellationToken);
        }

        var dtoEntity = await DbContext.RestaurantTableReservations
            .AsNoTracking()
            .Include(x => x.TableResource)
            .FirstAsync(x => x.Id == entity.Id, cancellationToken);

        return Ok(ToDto(dtoEntity));
    }

    [HttpPost("{reservationId:long}/reject")]
    public async Task<ActionResult<RestaurantTableReservationDto>> Reject(
    [FromRoute] long reservationId,
    [FromBody] RejectRestaurantTableReservationRequest request,
    CancellationToken cancellationToken)
    {
        var entity = await LoadReservationForWriteAsync(reservationId, cancellationToken);

        if (entity is null)
            return NotFound("Rezervacija ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(entity.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (entity.Status != RestaurantTableReservationStatus.PendingApproval)
            return BadRequest("Samo rezervacija koja čeka potvrdu može da se odbije.");

        var now = DateTime.UtcNow;

        entity.Status = RestaurantTableReservationStatus.Rejected;
        entity.RespondedAtUtc = now;
        entity.UpdatedAtUtc = now;
        entity.InternalNote = AppendText(entity.InternalNote, request.InternalNote, 1000);

        await DbContext.SaveChangesAsync(cancellationToken);
        await _systemAlarmService.CancelRestaurantTableShouldBeFreeAlarmForReservationAsync(
    entity.BusinessId,
    entity.Id,
    cancellationToken);

        return Ok(ToDto(entity));
    }

    [HttpPost("{reservationId:long}/cancel")]
    public async Task<ActionResult<RestaurantTableReservationDto>> Cancel(
        [FromRoute] long reservationId,
        [FromBody] CancelRestaurantTableReservationRequest request,
        CancellationToken cancellationToken)
    {
        var entity = await LoadReservationForWriteAsync(reservationId, cancellationToken);

        if (entity is null)
            return NotFound("Rezervacija ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(entity.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (entity.Status is RestaurantTableReservationStatus.Arrived
            or RestaurantTableReservationStatus.Cancelled
            or RestaurantTableReservationStatus.Rejected
            or RestaurantTableReservationStatus.NoShow)
        {
            return BadRequest("Ova rezervacija ne može da se otkaže.");
        }

        var now = DateTime.UtcNow;

        entity.Status = RestaurantTableReservationStatus.Cancelled;
        entity.CancelledAtUtc = now;
        entity.Note = AppendText(entity.Note, request.Note, 1000);
        entity.InternalNote = AppendText(entity.InternalNote, request.InternalNote, 1000);
        entity.UpdatedAtUtc = now;
        await DbContext.SaveChangesAsync(cancellationToken);

        await _systemAlarmService.CancelRestaurantTableShouldBeFreeAlarmForReservationAsync(
            entity.BusinessId,
            entity.Id,
            cancellationToken);

        return Ok(ToDto(entity));
    }

    [HttpPost("{reservationId:long}/mark-no-show")]
    public async Task<ActionResult<RestaurantTableReservationDto>> MarkNoShow(
        [FromRoute] long reservationId,
        [FromBody] MarkRestaurantTableReservationNoShowRequest request,
        CancellationToken cancellationToken)
    {
        var entity = await LoadReservationForWriteAsync(reservationId, cancellationToken);

        if (entity is null)
            return NotFound("Rezervacija ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(entity.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (entity.Status != RestaurantTableReservationStatus.Confirmed)
            return BadRequest("Samo potvrđena rezervacija može da se označi kao nedolazak.");

        entity.Status = RestaurantTableReservationStatus.NoShow;
        entity.InternalNote = AppendText(entity.InternalNote, request.InternalNote, 1000);
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await DbContext.SaveChangesAsync(cancellationToken);
        await _systemAlarmService.CancelRestaurantTableShouldBeFreeAlarmForReservationAsync(
    entity.BusinessId,
    entity.Id,
    cancellationToken);

        return Ok(ToDto(entity));
    }

    [HttpPost("{reservationId:long}/mark-arrived")]
    public async Task<ActionResult<RestaurantTableReservationDto>> MarkArrived(
        [FromRoute] long reservationId,
        [FromBody] MarkRestaurantTableReservationArrivedRequest request,
        CancellationToken cancellationToken)
    {
        var entity = await LoadReservationForWriteAsync(reservationId, cancellationToken);

        if (entity is null)
            return NotFound("Rezervacija ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(entity.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (entity.Status != RestaurantTableReservationStatus.Confirmed)
            return BadRequest("Samo potvrđena rezervacija može da se označi kao došla.");

        var tableResourceId = request.TableResourceId ?? entity.TableResourceId;

        if (!tableResourceId.HasValue)
            return BadRequest("Za dolazak gosta potrebno je izabrati sto.");

        var tableValidation = await ValidateTableAsync(
            entity.BusinessId,
            entity.RestaurantAreaId,
            tableResourceId.Value,
            entity.PartySize,
            cancellationToken);

        if (tableValidation is not null)
            return tableValidation;

        var areaConflictResult = await ValidateAreaReservationConflictForTableReservationAsync(
    entity.BusinessId,
    entity.RestaurantAreaId,
    entity.ReservationAtUtc,
    entity.ExpectedDurationMin,
    cancellationToken);

        if (areaConflictResult is not null)
            return areaConflictResult;

        var conflictResult = await ValidateReservationConflictAsync(
    entity.BusinessId,
    tableResourceId.Value,
    entity.ReservationAtUtc,
    entity.ExpectedDurationMin,
    entity.Id,
    cancellationToken);

        if (conflictResult is not null)
            return conflictResult;

        var tableAlreadyActive = await DbContext.RestaurantTableSessions
            .AsNoTracking()
            .AnyAsync(x =>
                x.BusinessId == entity.BusinessId &&
                x.TableResourceId == tableResourceId.Value &&
                x.Status == RestaurantTableSessionStatus.Active &&
                x.ReleasedAtUtc == null,
                cancellationToken);

        if (tableAlreadyActive)
            return BadRequest("Izabrani sto je trenutno zauzet.");

        var now = DateTime.UtcNow;

        var session = new RestaurantTableSession
        {
            BusinessId = entity.BusinessId,
            RestaurantAreaId = entity.RestaurantAreaId,
            TableResourceId = tableResourceId.Value,
            PartySize = entity.PartySize,
            CustomerName = entity.CustomerName,
            CustomerPhone = entity.CustomerPhone,
            Note = NormalizeText(request.InternalNote, 1000),
            Status = RestaurantTableSessionStatus.Active,
            StartedAtUtc = now,
            ReleasedAtUtc = null,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        DbContext.RestaurantTableSessions.Add(session);

        entity.Status = RestaurantTableReservationStatus.Arrived;
        entity.TableResourceId = tableResourceId.Value;
        entity.ArrivedAtUtc = now;
        entity.InternalNote = AppendText(entity.InternalNote, request.InternalNote, 1000);
        entity.UpdatedAtUtc = now;

        await DbContext.SaveChangesAsync(cancellationToken);

        entity.CreatedTableSessionId = session.Id;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await DbContext.SaveChangesAsync(cancellationToken);

        await _systemAlarmService.CancelRestaurantTableShouldBeFreeAlarmForReservationAsync(
    entity.BusinessId,
    entity.Id,
    cancellationToken);

        var dtoEntity = await DbContext.RestaurantTableReservations
            .AsNoTracking()
            .Include(x => x.TableResource)
            .FirstAsync(x => x.Id == entity.Id, cancellationToken);

        return Ok(ToDto(dtoEntity));
    }

    [HttpPost("{reservationId:long}/create-session")]
    public async Task<ActionResult<RestaurantTableSessionDto>> CreateSessionFromReservation(
        [FromRoute] long reservationId,
        [FromBody] CreateSessionFromRestaurantReservationRequest request,
        CancellationToken cancellationToken)
    {
        var entity = await LoadReservationForWriteAsync(reservationId, cancellationToken);

        if (entity is null)
            return NotFound("Rezervacija ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(entity.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (entity.Status != RestaurantTableReservationStatus.Confirmed)
            return BadRequest("Samo potvrđena rezervacija može da napravi zauzeće stola.");

        var tableResourceId = request.TableResourceId ?? entity.TableResourceId;

        if (!tableResourceId.HasValue)
            return BadRequest("Za zauzeće je potrebno izabrati sto.");

        var tableValidation = await ValidateTableAsync(
            entity.BusinessId,
            entity.RestaurantAreaId,
            tableResourceId.Value,
            entity.PartySize,
            cancellationToken);

        if (tableValidation is not null)
            return tableValidation;

        var areaConflictResult = await ValidateAreaReservationConflictForTableReservationAsync(
    entity.BusinessId,
    entity.RestaurantAreaId,
    entity.ReservationAtUtc,
    entity.ExpectedDurationMin,
    cancellationToken);

        if (areaConflictResult is not null)
            return areaConflictResult;

        var conflictResult = await ValidateReservationConflictAsync(
    entity.BusinessId,
    tableResourceId.Value,
    entity.ReservationAtUtc,
    entity.ExpectedDurationMin,
    entity.Id,
    cancellationToken);

        if (conflictResult is not null)
            return conflictResult;

        var tableAlreadyActive = await DbContext.RestaurantTableSessions
            .AsNoTracking()
            .AnyAsync(x =>
                x.BusinessId == entity.BusinessId &&
                x.TableResourceId == tableResourceId.Value &&
                x.Status == RestaurantTableSessionStatus.Active &&
                x.ReleasedAtUtc == null,
                cancellationToken);

        if (tableAlreadyActive)
            return BadRequest("Izabrani sto je trenutno zauzet.");

        var now = DateTime.UtcNow;

        var session = new RestaurantTableSession
        {
            BusinessId = entity.BusinessId,
            RestaurantAreaId = entity.RestaurantAreaId,
            TableResourceId = tableResourceId.Value,
            PartySize = entity.PartySize,
            CustomerName = entity.CustomerName,
            CustomerPhone = entity.CustomerPhone,
            Note = NormalizeText(request.Note, 1000),
            Status = RestaurantTableSessionStatus.Active,
            StartedAtUtc = now,
            ReleasedAtUtc = null,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        DbContext.RestaurantTableSessions.Add(session);

        entity.Status = RestaurantTableReservationStatus.Arrived;
        entity.TableResourceId = tableResourceId.Value;
        entity.ArrivedAtUtc = now;
        entity.UpdatedAtUtc = now;

        await DbContext.SaveChangesAsync(cancellationToken);

        entity.CreatedTableSessionId = session.Id;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await DbContext.SaveChangesAsync(cancellationToken);

        await _systemAlarmService.CancelRestaurantTableShouldBeFreeAlarmForReservationAsync(
    entity.BusinessId,
    entity.Id,
    cancellationToken);

        var dtoSession = await DbContext.RestaurantTableSessions
            .AsNoTracking()
            .Include(x => x.TableResource)
            .FirstAsync(x => x.Id == session.Id, cancellationToken);

        return Ok(ToSessionDto(dtoSession));
    }

    private async Task CreateTableShouldBeFreeAlarmIfNeededAsync(
    RestaurantTableReservation reservation,
    CancellationToken cancellationToken)
    {
        if (reservation.Status != RestaurantTableReservationStatus.Confirmed)
            return;

        if (!reservation.TableResourceId.HasValue)
            return;

        var reservationAtUtc = EnsureUtc(reservation.ReservationAtUtc);

        if (reservationAtUtc <= DateTime.UtcNow)
            return;

        var activeSession = await DbContext.RestaurantTableSessions
            .AsNoTracking()
            .Include(x => x.TableResource)
            .Where(x =>
                x.BusinessId == reservation.BusinessId &&
                x.RestaurantAreaId == reservation.RestaurantAreaId &&
                x.TableResourceId == reservation.TableResourceId.Value &&
                x.Status == RestaurantTableSessionStatus.Active &&
                x.ReleasedAtUtc == null)
            .OrderByDescending(x => x.StartedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (activeSession is null)
            return;

        var triggerAtUtc = reservationAtUtc.AddMinutes(-TableShouldBeFreeAlarmLeadMinutes);

        if (triggerAtUtc < DateTime.UtcNow)
            triggerAtUtc = DateTime.UtcNow;

        var targetRestaurantOperationUnitId = await GetDefaultRestaurantOperationUnitIdAsync(
            reservation.BusinessId,
            cancellationToken);

        var tableName = reservation.TableResource?.Name
            ?? activeSession.TableResource?.Name
            ?? $"Sto #{reservation.TableResourceId.Value}";

        await _systemAlarmService.CreateRestaurantTableShouldBeFreeAlarmAsync(
            reservation.BusinessId,
            reservation.Id,
            activeSession.Id,
            reservation.TableResourceId.Value,
            tableName,
            reservationAtUtc,
            reservation.PartySize,
            triggerAtUtc,
            targetRestaurantOperationUnitId,
            cancellationToken);
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

    private async Task<ActionResult?> ValidateAreaReservationConflictForTableReservationAsync(
    long businessId,
    long restaurantAreaId,
    DateTime reservationAtUtc,
    int? expectedDurationMin,
    CancellationToken cancellationToken)
    {
        var durationMin = expectedDurationMin.GetValueOrDefault(120);

        if (durationMin <= 0)
            durationMin = 120;

        var newStartUtc = EnsureUtc(reservationAtUtc);
        var newEndUtc = newStartUtc.AddMinutes(durationMin);

        var areaReservations = await DbContext.RestaurantAreaReservations
            .AsNoTracking()
            .Where(x =>
                x.BusinessId == businessId &&
                x.RestaurantAreaId == restaurantAreaId &&
                x.Status != RestaurantAreaReservationStatus.Rejected &&
                x.Status != RestaurantAreaReservationStatus.Cancelled &&
                x.Status != RestaurantAreaReservationStatus.NoShow &&
                x.Status != RestaurantAreaReservationStatus.Completed)
            .Select(x => new
            {
                x.Id,
                x.ReservationAtUtc,
                x.ExpectedDurationMin,
                x.CustomerName,
                x.Status
            })
            .ToListAsync(cancellationToken);

        foreach (var reservation in areaReservations)
        {
            var existingStartUtc = EnsureUtc(reservation.ReservationAtUtc);
            var existingDurationMin = reservation.ExpectedDurationMin.GetValueOrDefault(240);

            if (existingDurationMin <= 0)
                existingDurationMin = 240;

            var existingEndUtc = existingStartUtc.AddMinutes(existingDurationMin);

            var overlaps = existingStartUtc < newEndUtc && newStartUtc < existingEndUtc;

            if (!overlaps)
                continue;

            return BadRequest(
                $"Cela sala je već rezervisana u tom periodu. Postojeća rezervacija: {reservation.CustomerName}, {existingStartUtc:yyyy-MM-dd HH:mm} UTC.");
        }

        return null;
    }

    private async Task<ActionResult?> ValidateReservationConflictAsync(
    long businessId,
    long? tableResourceId,
    DateTime reservationAtUtc,
    int? expectedDurationMin,
    long? currentReservationId,
    CancellationToken cancellationToken)
    {
        if (!tableResourceId.HasValue)
            return null;

        var durationMin = expectedDurationMin.GetValueOrDefault(120);

        if (durationMin <= 0)
            durationMin = 120;

        var newStartUtc = EnsureUtc(reservationAtUtc);
        var newEndUtc = newStartUtc.AddMinutes(durationMin);

        var reservations = await DbContext.RestaurantTableReservations
            .AsNoTracking()
            .Where(x =>
                x.BusinessId == businessId &&
                x.TableResourceId == tableResourceId.Value &&
                x.Status != RestaurantTableReservationStatus.Rejected &&
                x.Status != RestaurantTableReservationStatus.Cancelled &&
                x.Status != RestaurantTableReservationStatus.NoShow &&
                x.Status != RestaurantTableReservationStatus.Arrived &&
                (!currentReservationId.HasValue || x.Id != currentReservationId.Value))
                    .Select(x => new
            {
                x.Id,
                x.ReservationAtUtc,
                x.ExpectedDurationMin,
                x.CustomerName,
                x.Status
            })
            .ToListAsync(cancellationToken);

        foreach (var reservation in reservations)
        {
            var existingStartUtc = EnsureUtc(reservation.ReservationAtUtc);
            var existingDurationMin = reservation.ExpectedDurationMin.GetValueOrDefault(120);

            if (existingDurationMin <= 0)
                existingDurationMin = 120;

            var existingEndUtc = existingStartUtc.AddMinutes(existingDurationMin);

            var overlaps = existingStartUtc < newEndUtc && newStartUtc < existingEndUtc;

            if (!overlaps)
                continue;

            return BadRequest(
                $"Izabrani sto je već rezervisan u tom periodu. Postojeća rezervacija: {reservation.CustomerName}, {existingStartUtc:yyyy-MM-dd HH:mm} UTC.");
        }

        return null;
    }

    private async Task<RestaurantTableReservation?> LoadReservationForWriteAsync(
        long reservationId,
        CancellationToken cancellationToken)
    {
        return await DbContext.RestaurantTableReservations
            .Include(x => x.TableResource)
            .FirstOrDefaultAsync(x => x.Id == reservationId, cancellationToken);
    }

    private async Task<ActionResult?> ValidateReservationRequestAsync(
        long businessId,
        long restaurantAreaId,
        long? tableResourceId,
        int partySize,
        string customerName,
        string customerPhone,
        DateTime reservationAtUtc,
        int? expectedDurationMin,
        CancellationToken cancellationToken)
    {
        if (restaurantAreaId <= 0)
            return BadRequest("restaurantAreaId je obavezan.");

        if (partySize <= 0)
            return BadRequest("Broj gostiju mora biti veći od 0.");

        if (string.IsNullOrWhiteSpace(customerName))
            return BadRequest("Unesite ime gosta.");

        if (string.IsNullOrWhiteSpace(customerPhone))
            return BadRequest("Unesite telefon gosta.");

        var reservationUtc = EnsureUtc(reservationAtUtc);

        if (reservationUtc < DateTime.UtcNow.AddMinutes(-5))
            return BadRequest("Vreme rezervacije ne može biti u prošlosti.");

        if (expectedDurationMin.HasValue && expectedDurationMin.Value <= 0)
            return BadRequest("Očekivano trajanje mora biti veće od 0 minuta.");

        var areaExists = await DbContext.RestaurantAreas
            .AsNoTracking()
            .AnyAsync(x =>
                x.Id == restaurantAreaId &&
                x.BusinessId == businessId &&
                x.IsActive,
                cancellationToken);

        if (!areaExists)
            return BadRequest("Izabrana sala ne postoji ili nije aktivna.");

        if (tableResourceId.HasValue)
        {
            var tableValidation = await ValidateTableAsync(
                businessId,
                restaurantAreaId,
                tableResourceId.Value,
                partySize,
                cancellationToken);

            if (tableValidation is not null)
                return tableValidation;
        }

        return null;
    }

    private async Task<ActionResult?> ValidateTableAsync(
        long businessId,
        long restaurantAreaId,
        long tableResourceId,
        int partySize,
        CancellationToken cancellationToken)
    {
        var table = await DbContext.Resources
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.Id == tableResourceId &&
                x.BusinessId == businessId &&
                x.RestaurantAreaId == restaurantAreaId &&
                x.IsActive,
                cancellationToken);

        if (table is null)
            return BadRequest("Izabrani sto ne postoji ili ne pripada ovoj sali.");

        if (table.ResourceType != ResourceType.DiningTable && table.ResourceType != ResourceType.Table)
            return BadRequest("Izabrani resurs nije sto.");

        if (table.Capacity.HasValue && partySize > table.Capacity.Value)
            return BadRequest("Broj gostiju je veći od kapaciteta stola.");

        return null;
    }

    private static RestaurantTableReservationDto ToDto(RestaurantTableReservation entity)
    {
        return new RestaurantTableReservationDto
        {
            Id = entity.Id,
            BusinessId = entity.BusinessId,
            RestaurantAreaId = entity.RestaurantAreaId,
            TableResourceId = entity.TableResourceId,
            TableName = entity.TableResource?.Name,
            PartySize = entity.PartySize,
            CustomerName = entity.CustomerName,
            CustomerPhone = entity.CustomerPhone,
            CustomerEmail = entity.CustomerEmail,
            ReservationAtUtc = entity.ReservationAtUtc,
            ExpectedDurationMin = entity.ExpectedDurationMin,
            Status = (int)entity.Status,
            StatusText = GetStatusText(entity.Status),
            Note = entity.Note,
            InternalNote = entity.InternalNote,
            RespondedAtUtc = entity.RespondedAtUtc,
            CancelledAtUtc = entity.CancelledAtUtc,
            ArrivedAtUtc = entity.ArrivedAtUtc,
            CreatedTableSessionId = entity.CreatedTableSessionId,
            CreatedAtUtc = entity.CreatedAtUtc,
            UpdatedAtUtc = entity.UpdatedAtUtc
        };
    }

    private static RestaurantTableSessionDto ToSessionDto(RestaurantTableSession entity)
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
            StatusText = GetSessionStatusText(entity.Status),
            StartedAtUtc = entity.StartedAtUtc,
            ReleasedAtUtc = entity.ReleasedAtUtc
        };
    }

    private static string GetStatusText(RestaurantTableReservationStatus status)
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

    private static string GetSessionStatusText(RestaurantTableSessionStatus status)
    {
        return status switch
        {
            RestaurantTableSessionStatus.Active => "Zauzeto",
            RestaurantTableSessionStatus.Released => "Oslobođeno",
            RestaurantTableSessionStatus.Cancelled => "Otkazano",
            _ => "Nepoznat status"
        };
    }

    private static string NormalizeRequiredText(string value)
    {
        return value.Trim();
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

    private static string? AppendText(string? existing, string? value, int maxLength)
    {
        var normalized = NormalizeText(value, maxLength);

        if (string.IsNullOrWhiteSpace(normalized))
            return existing;

        var result = string.IsNullOrWhiteSpace(existing)
            ? normalized
            : $"{existing}\n{normalized}";

        return result.Length <= maxLength
            ? result
            : result[..maxLength];
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
}