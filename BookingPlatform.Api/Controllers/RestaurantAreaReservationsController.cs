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
public sealed class RestaurantAreaReservationsController : ApiControllerBase
{
    public RestaurantAreaReservationsController(BookingDbContext dbContext) : base(dbContext)
    {
    }

    [HttpGet]
    public async Task<ActionResult<List<RestaurantAreaReservationDto>>> GetAll(
        [FromQuery] long businessId,
        [FromQuery] long? restaurantAreaId,
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

        var query = DbContext.RestaurantAreaReservations
            .AsNoTracking()
            .Include(x => x.RestaurantArea)
            .Where(x => x.BusinessId == businessId);

        if (restaurantAreaId.HasValue)
            query = query.Where(x => x.RestaurantAreaId == restaurantAreaId.Value);

        if (status.HasValue)
        {
            if (!Enum.IsDefined(typeof(RestaurantAreaReservationStatus), status.Value))
                return BadRequest("Nepoznat status rezervacije sale.");

            query = query.Where(x => x.Status == (RestaurantAreaReservationStatus)status.Value);
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

    [HttpGet("{reservationId:long}")]
    public async Task<ActionResult<RestaurantAreaReservationDto>> GetById(
        [FromRoute] long reservationId,
        CancellationToken cancellationToken)
    {
        var entity = await DbContext.RestaurantAreaReservations
            .AsNoTracking()
            .Include(x => x.RestaurantArea)
            .FirstOrDefaultAsync(x => x.Id == reservationId, cancellationToken);

        if (entity is null)
            return NotFound("Rezervacija sale ne postoji.");

        var accessResult = await EnsureBusinessReadAccessAsync(entity.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        return Ok(ToDto(entity));
    }

    [HttpGet("available-areas")]
    public async Task<ActionResult<List<AvailableRestaurantAreaDto>>> GetAvailableAreas(
    [FromQuery] long businessId,
    [FromQuery] int partySize,
    [FromQuery] DateTime reservationAtUtc,
    [FromQuery] int? expectedDurationMin,
    CancellationToken cancellationToken)
    {
        if (businessId <= 0)
            return BadRequest("businessId je obavezan.");

        if (partySize <= 0)
            return BadRequest("Broj gostiju mora biti veći od 0.");

        var accessResult = await EnsureBusinessReadAccessAsync(businessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var reservationStartUtc = EnsureUtc(reservationAtUtc);

        if (reservationStartUtc < DateTime.UtcNow.AddMinutes(-5))
            return BadRequest("Vreme rezervacije ne može biti u prošlosti.");

        var durationMin = expectedDurationMin.GetValueOrDefault(240);

        if (durationMin <= 0)
            durationMin = 240;

        var reservationEndUtc = reservationStartUtc.AddMinutes(durationMin);

        var areas = await DbContext.RestaurantAreas
            .AsNoTracking()
            .Where(x =>
                x.BusinessId == businessId &&
                x.IsActive &&
                x.IsReservableAsWhole &&
                (!x.Capacity.HasValue || x.Capacity.Value >= partySize))
            .OrderBy(x => x.Capacity ?? int.MaxValue)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        if (areas.Count == 0)
            return Ok(new List<AvailableRestaurantAreaDto>());

        var areaIds = areas
            .Select(x => x.Id)
            .ToList();

        var areaReservations = await DbContext.RestaurantAreaReservations
            .AsNoTracking()
            .Where(x =>
                x.BusinessId == businessId &&
                areaIds.Contains(x.RestaurantAreaId) &&
                x.Status != RestaurantAreaReservationStatus.Rejected &&
                x.Status != RestaurantAreaReservationStatus.Cancelled &&
                x.Status != RestaurantAreaReservationStatus.NoShow &&
                x.Status != RestaurantAreaReservationStatus.Completed)
            .Select(x => new
            {
                x.RestaurantAreaId,
                x.ReservationAtUtc,
                x.ExpectedDurationMin
            })
            .ToListAsync(cancellationToken);

        var unavailableAreaIds = new HashSet<long>();

        foreach (var existing in areaReservations)
        {
            var existingStartUtc = EnsureUtc(existing.ReservationAtUtc);
            var existingDurationMin = existing.ExpectedDurationMin.GetValueOrDefault(240);

            if (existingDurationMin <= 0)
                existingDurationMin = 240;

            var existingEndUtc = existingStartUtc.AddMinutes(existingDurationMin);

            var overlaps = existingStartUtc < reservationEndUtc &&
                           reservationStartUtc < existingEndUtc;

            if (overlaps)
                unavailableAreaIds.Add(existing.RestaurantAreaId);
        }

        var tableReservations = await DbContext.RestaurantTableReservations
            .AsNoTracking()
            .Where(x =>
                x.BusinessId == businessId &&
                areaIds.Contains(x.RestaurantAreaId) &&
                x.Status != RestaurantTableReservationStatus.Rejected &&
                x.Status != RestaurantTableReservationStatus.Cancelled &&
                x.Status != RestaurantTableReservationStatus.NoShow &&
                x.Status != RestaurantTableReservationStatus.Arrived)
            .Select(x => new
            {
                x.RestaurantAreaId,
                x.ReservationAtUtc,
                x.ExpectedDurationMin
            })
            .ToListAsync(cancellationToken);

        foreach (var existing in tableReservations)
        {
            var existingStartUtc = EnsureUtc(existing.ReservationAtUtc);
            var existingDurationMin = existing.ExpectedDurationMin.GetValueOrDefault(120);

            if (existingDurationMin <= 0)
                existingDurationMin = 120;

            var existingEndUtc = existingStartUtc.AddMinutes(existingDurationMin);

            var overlaps = existingStartUtc < reservationEndUtc &&
                           reservationStartUtc < existingEndUtc;

            if (overlaps)
                unavailableAreaIds.Add(existing.RestaurantAreaId);
        }

        var activeSessionAreaIds = await DbContext.RestaurantTableSessions
            .AsNoTracking()
            .Where(x =>
                x.BusinessId == businessId &&
                areaIds.Contains(x.RestaurantAreaId) &&
                x.Status == RestaurantTableSessionStatus.Active &&
                x.ReleasedAtUtc == null)
            .Select(x => x.RestaurantAreaId)
            .Distinct()
            .ToListAsync(cancellationToken);

        foreach (var areaId in activeSessionAreaIds)
            unavailableAreaIds.Add(areaId);

        var availableAreas = areas
            .Where(x => !unavailableAreaIds.Contains(x.Id))
            .Select(x =>
            {
                var capacity = x.Capacity ?? partySize;
                var capacityDifference = capacity - partySize;

                return new AvailableRestaurantAreaDto
                {
                    RestaurantAreaId = x.Id,
                    AreaName = x.Name,
                    Capacity = x.Capacity,
                    CapacityDifference = capacityDifference,
                    CanvasWidth = x.CanvasWidth,
                    CanvasHeight = x.CanvasHeight,
                    BoundaryPointsJson = x.BoundaryPointsJson
                };
            })
            .OrderBy(x => x.CapacityDifference)
            .ThenBy(x => x.AreaName)
            .ToList();

        var bestCapacityDifference = availableAreas.Count == 0
            ? (int?)null
            : availableAreas.Min(x => x.CapacityDifference);

        foreach (var area in availableAreas)
        {
            area.IsBestFit = bestCapacityDifference.HasValue &&
                             area.CapacityDifference == bestCapacityDifference.Value;
        }

        return Ok(availableAreas);
    }

    [HttpPost]
    public async Task<ActionResult<RestaurantAreaReservationDto>> Create(
        [FromBody] CreateRestaurantAreaReservationRequest request,
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
            request.PartySize,
            request.CustomerName,
            request.CustomerPhone,
            request.ReservationAtUtc,
            request.ExpectedDurationMin,
            cancellationToken);

        if (validationResult is not null)
            return validationResult;

        var conflictResult = await ValidateAreaReservationConflictAsync(
            request.BusinessId,
            request.RestaurantAreaId,
            EnsureUtc(request.ReservationAtUtc),
            request.ExpectedDurationMin,
            null,
            cancellationToken);

        if (conflictResult is not null)
            return conflictResult;

        var now = DateTime.UtcNow;

        var entity = new RestaurantAreaReservation
        {
            BusinessId = request.BusinessId,
            RestaurantAreaId = request.RestaurantAreaId,
            PartySize = request.PartySize,
            CustomerName = NormalizeRequiredText(request.CustomerName),
            CustomerPhone = NormalizeRequiredText(request.CustomerPhone),
            CustomerEmail = NormalizeText(request.CustomerEmail, 200),
            ReservationAtUtc = EnsureUtc(request.ReservationAtUtc),
            ExpectedDurationMin = request.ExpectedDurationMin,
            Status = RestaurantAreaReservationStatus.PendingApproval,
            Note = NormalizeText(request.Note, 1000),
            InternalNote = NormalizeText(request.InternalNote, 1000),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        DbContext.RestaurantAreaReservations.Add(entity);
        await DbContext.SaveChangesAsync(cancellationToken);

        var dtoEntity = await DbContext.RestaurantAreaReservations
            .AsNoTracking()
            .Include(x => x.RestaurantArea)
            .FirstAsync(x => x.Id == entity.Id, cancellationToken);

        return Ok(ToDto(dtoEntity));
    }

    [HttpPut("{reservationId:long}")]
    public async Task<ActionResult<RestaurantAreaReservationDto>> Update(
        [FromRoute] long reservationId,
        [FromBody] UpdateRestaurantAreaReservationRequest request,
        CancellationToken cancellationToken)
    {
        var entity = await LoadReservationForWriteAsync(reservationId, cancellationToken);

        if (entity is null)
            return NotFound("Rezervacija sale ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(entity.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (entity.Status is RestaurantAreaReservationStatus.Arrived
            or RestaurantAreaReservationStatus.Completed
            or RestaurantAreaReservationStatus.Cancelled
            or RestaurantAreaReservationStatus.Rejected
            or RestaurantAreaReservationStatus.NoShow)
        {
            return BadRequest("Ova rezervacija sale više ne može da se menja.");
        }

        var validationResult = await ValidateReservationRequestAsync(
            entity.BusinessId,
            request.RestaurantAreaId,
            request.PartySize,
            request.CustomerName,
            request.CustomerPhone,
            request.ReservationAtUtc,
            request.ExpectedDurationMin,
            cancellationToken);

        if (validationResult is not null)
            return validationResult;

        var conflictResult = await ValidateAreaReservationConflictAsync(
            entity.BusinessId,
            request.RestaurantAreaId,
            EnsureUtc(request.ReservationAtUtc),
            request.ExpectedDurationMin,
            entity.Id,
            cancellationToken);

        if (conflictResult is not null)
            return conflictResult;

        entity.RestaurantAreaId = request.RestaurantAreaId;
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

        var dtoEntity = await DbContext.RestaurantAreaReservations
            .AsNoTracking()
            .Include(x => x.RestaurantArea)
            .FirstAsync(x => x.Id == entity.Id, cancellationToken);

        return Ok(ToDto(dtoEntity));
    }

    [HttpDelete("{reservationId:long}")]
    public async Task<ActionResult> Delete(
        [FromRoute] long reservationId,
        CancellationToken cancellationToken)
    {
        var entity = await DbContext.RestaurantAreaReservations
            .FirstOrDefaultAsync(x => x.Id == reservationId, cancellationToken);

        if (entity is null)
            return NotFound("Rezervacija sale ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(entity.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (entity.Status is RestaurantAreaReservationStatus.Arrived
            or RestaurantAreaReservationStatus.Completed)
        {
            return BadRequest("Rezervacija sale koja je već realizovana ne može da se obriše.");
        }

        DbContext.RestaurantAreaReservations.Remove(entity);
        await DbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpPost("{reservationId:long}/confirm")]
    public async Task<ActionResult<RestaurantAreaReservationDto>> Confirm(
        [FromRoute] long reservationId,
        CancellationToken cancellationToken)
    {
        var entity = await LoadReservationForWriteAsync(reservationId, cancellationToken);

        if (entity is null)
            return NotFound("Rezervacija sale ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(entity.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (entity.Status != RestaurantAreaReservationStatus.PendingApproval)
            return BadRequest("Samo rezervacija sale koja čeka potvrdu može da se potvrdi.");

        var conflictResult = await ValidateAreaReservationConflictAsync(
            entity.BusinessId,
            entity.RestaurantAreaId,
            entity.ReservationAtUtc,
            entity.ExpectedDurationMin,
            entity.Id,
            cancellationToken);

        if (conflictResult is not null)
            return conflictResult;

        var now = DateTime.UtcNow;

        entity.Status = RestaurantAreaReservationStatus.Confirmed;
        entity.RespondedAtUtc = now;
        entity.UpdatedAtUtc = now;

        await DbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(entity));
    }

    [HttpPost("{reservationId:long}/reject")]
    public async Task<ActionResult<RestaurantAreaReservationDto>> Reject(
        [FromRoute] long reservationId,
        [FromBody] RejectRestaurantAreaReservationRequest request,
        CancellationToken cancellationToken)
    {
        var entity = await LoadReservationForWriteAsync(reservationId, cancellationToken);

        if (entity is null)
            return NotFound("Rezervacija sale ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(entity.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (entity.Status != RestaurantAreaReservationStatus.PendingApproval)
            return BadRequest("Samo rezervacija sale koja čeka potvrdu može da se odbije.");

        var now = DateTime.UtcNow;

        entity.Status = RestaurantAreaReservationStatus.Rejected;
        entity.RespondedAtUtc = now;
        entity.InternalNote = AppendText(entity.InternalNote, request.InternalNote, 1000);
        entity.UpdatedAtUtc = now;

        await DbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(entity));
    }

    [HttpPost("{reservationId:long}/cancel")]
    public async Task<ActionResult<RestaurantAreaReservationDto>> Cancel(
        [FromRoute] long reservationId,
        [FromBody] CancelRestaurantAreaReservationRequest request,
        CancellationToken cancellationToken)
    {
        var entity = await LoadReservationForWriteAsync(reservationId, cancellationToken);

        if (entity is null)
            return NotFound("Rezervacija sale ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(entity.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (entity.Status is RestaurantAreaReservationStatus.Arrived
            or RestaurantAreaReservationStatus.Completed
            or RestaurantAreaReservationStatus.Cancelled
            or RestaurantAreaReservationStatus.Rejected
            or RestaurantAreaReservationStatus.NoShow)
        {
            return BadRequest("Ova rezervacija sale ne može da se otkaže.");
        }

        var now = DateTime.UtcNow;

        entity.Status = RestaurantAreaReservationStatus.Cancelled;
        entity.CancelledAtUtc = now;
        entity.Note = AppendText(entity.Note, request.Note, 1000);
        entity.InternalNote = AppendText(entity.InternalNote, request.InternalNote, 1000);
        entity.UpdatedAtUtc = now;

        await DbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(entity));
    }

    [HttpPost("{reservationId:long}/mark-arrived")]
    public async Task<ActionResult<RestaurantAreaReservationDto>> MarkArrived(
        [FromRoute] long reservationId,
        [FromBody] MarkRestaurantAreaReservationArrivedRequest request,
        CancellationToken cancellationToken)
    {
        var entity = await LoadReservationForWriteAsync(reservationId, cancellationToken);

        if (entity is null)
            return NotFound("Rezervacija sale ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(entity.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (entity.Status != RestaurantAreaReservationStatus.Confirmed)
            return BadRequest("Samo potvrđena rezervacija sale može da se označi kao došla.");

        var now = DateTime.UtcNow;

        entity.Status = RestaurantAreaReservationStatus.Arrived;
        entity.ArrivedAtUtc = now;
        entity.InternalNote = AppendText(entity.InternalNote, request.InternalNote, 1000);
        entity.UpdatedAtUtc = now;

        await DbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(entity));
    }

    [HttpPost("{reservationId:long}/mark-no-show")]
    public async Task<ActionResult<RestaurantAreaReservationDto>> MarkNoShow(
        [FromRoute] long reservationId,
        [FromBody] MarkRestaurantAreaReservationNoShowRequest request,
        CancellationToken cancellationToken)
    {
        var entity = await LoadReservationForWriteAsync(reservationId, cancellationToken);

        if (entity is null)
            return NotFound("Rezervacija sale ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(entity.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (entity.Status != RestaurantAreaReservationStatus.Confirmed)
            return BadRequest("Samo potvrđena rezervacija sale može da se označi kao nedolazak.");

        entity.Status = RestaurantAreaReservationStatus.NoShow;
        entity.InternalNote = AppendText(entity.InternalNote, request.InternalNote, 1000);
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await DbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(entity));
    }

    [HttpPost("{reservationId:long}/complete")]
    public async Task<ActionResult<RestaurantAreaReservationDto>> Complete(
        [FromRoute] long reservationId,
        [FromBody] CompleteRestaurantAreaReservationRequest request,
        CancellationToken cancellationToken)
    {
        var entity = await LoadReservationForWriteAsync(reservationId, cancellationToken);

        if (entity is null)
            return NotFound("Rezervacija sale ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(entity.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (entity.Status != RestaurantAreaReservationStatus.Arrived)
            return BadRequest("Samo započeta rezervacija sale može da se završi.");

        var now = DateTime.UtcNow;

        entity.Status = RestaurantAreaReservationStatus.Completed;
        entity.CompletedAtUtc = now;
        entity.InternalNote = AppendText(entity.InternalNote, request.InternalNote, 1000);
        entity.UpdatedAtUtc = now;

        await DbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(entity));
    }

    private async Task<RestaurantAreaReservation?> LoadReservationForWriteAsync(
        long reservationId,
        CancellationToken cancellationToken)
    {
        return await DbContext.RestaurantAreaReservations
            .Include(x => x.RestaurantArea)
            .FirstOrDefaultAsync(x => x.Id == reservationId, cancellationToken);
    }

    private async Task<ActionResult?> ValidateReservationRequestAsync(
        long businessId,
        long restaurantAreaId,
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

        var area = await DbContext.RestaurantAreas
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.Id == restaurantAreaId &&
                x.BusinessId == businessId &&
                x.IsActive,
                cancellationToken);

        if (area is null)
            return BadRequest("Izabrana sala ne postoji ili nije aktivna.");

        if (!area.IsReservableAsWhole)
            return BadRequest("Izabrana sala nije označena kao sala koja može da se rezerviše cela.");

        return null;
    }

    private async Task<ActionResult?> ValidateAreaReservationConflictAsync(
        long businessId,
        long restaurantAreaId,
        DateTime reservationAtUtc,
        int? expectedDurationMin,
        long? currentReservationId,
        CancellationToken cancellationToken)
    {
        var durationMin = expectedDurationMin.GetValueOrDefault(240);

        if (durationMin <= 0)
            durationMin = 240;

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
                x.Status != RestaurantAreaReservationStatus.Completed &&
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
                $"Sala je već rezervisana u tom periodu. Postojeća rezervacija: {reservation.CustomerName}, {existingStartUtc:yyyy-MM-dd HH:mm} UTC.");
        }

        var tableReservations = await DbContext.RestaurantTableReservations
            .AsNoTracking()
            .Where(x =>
                x.BusinessId == businessId &&
                x.RestaurantAreaId == restaurantAreaId &&
                x.Status != RestaurantTableReservationStatus.Rejected &&
                x.Status != RestaurantTableReservationStatus.Cancelled &&
                x.Status != RestaurantTableReservationStatus.NoShow &&
                x.Status != RestaurantTableReservationStatus.Arrived)
            .Select(x => new
            {
                x.Id,
                x.ReservationAtUtc,
                x.ExpectedDurationMin,
                x.CustomerName,
                x.Status
            })
            .ToListAsync(cancellationToken);

        foreach (var reservation in tableReservations)
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
                $"U ovoj sali već postoji rezervacija stola u tom periodu. Postojeća rezervacija: {reservation.CustomerName}, {existingStartUtc:yyyy-MM-dd HH:mm} UTC.");
        }

        return null;
    }

    private static RestaurantAreaReservationDto ToDto(RestaurantAreaReservation entity)
    {
        return new RestaurantAreaReservationDto
        {
            Id = entity.Id,
            BusinessId = entity.BusinessId,
            RestaurantAreaId = entity.RestaurantAreaId,
            RestaurantAreaName = entity.RestaurantArea.Name,
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
            CompletedAtUtc = entity.CompletedAtUtc,
            CreatedAtUtc = entity.CreatedAtUtc,
            UpdatedAtUtc = entity.UpdatedAtUtc
        };
    }

    private static string GetStatusText(RestaurantAreaReservationStatus status)
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