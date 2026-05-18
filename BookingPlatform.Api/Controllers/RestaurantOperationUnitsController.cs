using BookingPlatform.Contracts.Restaurants;
using BookingPlatform.Domain.Restaurants;
using BookingPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookingPlatform.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class RestaurantOperationUnitsController : ApiControllerBase
{
    public RestaurantOperationUnitsController(BookingDbContext dbContext)
        : base(dbContext)
    {
    }

    [HttpGet]
    public async Task<ActionResult<List<RestaurantOperationUnitDto>>> GetByBusiness(
        [FromQuery] long businessId,
        CancellationToken cancellationToken = default)
    {
        if (businessId <= 0)
            return BadRequest("businessId je obavezan.");

        var accessResult = await EnsureBusinessReadAccessAsync(businessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var items = await DbContext.RestaurantOperationUnits
            .AsNoTracking()
            .Include(x => x.WorkingHours)
            .Where(x => x.BusinessId == businessId)
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return Ok(items.Select(ToDto).ToList());
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<RestaurantOperationUnitDto>> GetById(
        [FromRoute] long id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            return BadRequest("Id je obavezan.");

        var item = await DbContext.RestaurantOperationUnits
            .AsNoTracking()
            .Include(x => x.WorkingHours)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (item is null)
            return NotFound("Radna jedinica restorana ne postoji.");

        var accessResult = await EnsureBusinessReadAccessAsync(item.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        return Ok(ToDto(item));
    }

    [HttpPost]
    public async Task<ActionResult<RestaurantOperationUnitDto>> Create(
        [FromBody] CreateRestaurantOperationUnitRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (request.BusinessId <= 0)
            return BadRequest("businessId je obavezan.");

        var accessResult = await EnsureBusinessWriteAccessAsync(request.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (!Enum.IsDefined(typeof(RestaurantOperationUnitType), request.UnitType))
            return BadRequest("Tip radne jedinice nije ispravan.");

        var unitType = (RestaurantOperationUnitType)request.UnitType;

        var name = string.IsNullOrWhiteSpace(request.Name)
            ? GetUnitTypeText(unitType)
            : request.Name.Trim();

        var alreadyExists = await DbContext.RestaurantOperationUnits
            .AnyAsync(x =>
                x.BusinessId == request.BusinessId &&
                x.Name.ToLower() == name.ToLower(),
                cancellationToken);

        if (alreadyExists)
            return BadRequest("Sektor sa ovim nazivom već postoji za izabrani restoran.");

        var entity = new RestaurantOperationUnit
        {
            BusinessId = request.BusinessId,
            UnitType = unitType,
            Name = name,
            IsActive = request.IsActive,
            DisplayOrder = request.DisplayOrder,
            ReceivesCustomerChat = request.ReceivesCustomerChat
        };

        DbContext.RestaurantOperationUnits.Add(entity);

        await DbContext.SaveChangesAsync(cancellationToken);

        var saved = await DbContext.RestaurantOperationUnits
            .AsNoTracking()
            .Include(x => x.WorkingHours)
            .FirstAsync(x => x.Id == entity.Id, cancellationToken);

        return Ok(ToDto(saved));
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<RestaurantOperationUnitDto>> Update(
        [FromRoute] long id,
        [FromBody] UpdateRestaurantOperationUnitRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            return BadRequest("Id je obavezan.");

        var entity = await DbContext.RestaurantOperationUnits
            .Include(x => x.WorkingHours)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
            return NotFound("Radna jedinica restorana ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(entity.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest("Naziv je obavezan.");

        entity.Name = request.Name.Trim();
        entity.IsActive = request.IsActive;
        entity.DisplayOrder = request.DisplayOrder;
        entity.ReceivesCustomerChat = request.ReceivesCustomerChat;

        await DbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(entity));
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(
        [FromRoute] long id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            return BadRequest("Id je obavezan.");

        var entity = await DbContext.RestaurantOperationUnits
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
            return NotFound("Radna jedinica restorana ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(entity.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        DbContext.RestaurantOperationUnits.Remove(entity);

        await DbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpPost("{id:long}/working-hours/replace")]
    public async Task<ActionResult<RestaurantOperationUnitDto>> ReplaceWorkingHours(
        [FromRoute] long id,
        [FromBody] ReplaceRestaurantOperationUnitWorkingHoursRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            return BadRequest("Id je obavezan.");

        var entity = await DbContext.RestaurantOperationUnits
            .Include(x => x.WorkingHours)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
            return NotFound("Radna jedinica restorana ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(entity.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (request.WorkingHours.Count == 0)
            return BadRequest("Radno vreme nije poslato.");

        var days = request.WorkingHours
            .Select(x => x.DayOfWeek)
            .ToList();

        if (days.Any(x => x < 1 || x > 7))
            return BadRequest("Dan u nedelji mora biti od 1 do 7.");

        if (days.Distinct().Count() != days.Count)
            return BadRequest("Dan u nedelji ne sme biti dupliran.");

        foreach (var item in request.WorkingHours)
        {
            if (!TimeSpan.TryParse(item.StartTime, out _))
                return BadRequest($"Početak radnog vremena nije ispravan za dan {item.DayOfWeek}.");

            if (!TimeSpan.TryParse(item.EndTime, out _))
                return BadRequest($"Kraj radnog vremena nije ispravan za dan {item.DayOfWeek}.");
        }

        DbContext.RestaurantOperationUnitWorkingHours.RemoveRange(entity.WorkingHours);

        foreach (var item in request.WorkingHours.OrderBy(x => x.DayOfWeek))
        {
            var startTime = TimeSpan.Parse(item.StartTime);
            var endTime = TimeSpan.Parse(item.EndTime);

            entity.WorkingHours.Add(new RestaurantOperationUnitWorkingHour
            {
                BusinessId = entity.BusinessId,
                OperationUnitId = entity.Id,
                DayOfWeek = item.DayOfWeek,
                StartTime = startTime,
                EndTime = endTime,
                IsClosed = item.IsClosed
            });
        }

        await DbContext.SaveChangesAsync(cancellationToken);

        var saved = await DbContext.RestaurantOperationUnits
            .AsNoTracking()
            .Include(x => x.WorkingHours)
            .FirstAsync(x => x.Id == id, cancellationToken);

        return Ok(ToDto(saved));
    }

    [HttpGet("{id:long}/status")]
    public async Task<ActionResult<RestaurantOperationUnitStatusDto>> GetStatus(
        [FromRoute] long id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            return BadRequest("Id je obavezan.");

        var entity = await DbContext.RestaurantOperationUnits
            .AsNoTracking()
            .Include(x => x.WorkingHours)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
            return NotFound("Radna jedinica restorana ne postoji.");

        var accessResult = await EnsureBusinessReadAccessAsync(entity.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        return Ok(ToStatusDto(entity));
    }

    private static RestaurantOperationUnitDto ToDto(RestaurantOperationUnit entity)
    {
        return new RestaurantOperationUnitDto
        {
            Id = entity.Id,
            BusinessId = entity.BusinessId,
            UnitType = (int)entity.UnitType,
            UnitTypeText = GetUnitTypeText(entity.UnitType),
            Name = entity.Name,
            IsActive = entity.IsActive,
            DisplayOrder = entity.DisplayOrder,
            ReceivesCustomerChat = entity.ReceivesCustomerChat,
            WorkingHours = entity.WorkingHours
                .OrderBy(x => x.DayOfWeek)
                .Select(ToWorkingHourDto)
                .ToList()
        };
    }

    private static RestaurantOperationUnitWorkingHourDto ToWorkingHourDto(
        RestaurantOperationUnitWorkingHour entity)
    {
        return new RestaurantOperationUnitWorkingHourDto
        {
            Id = entity.Id,
            OperationUnitId = entity.OperationUnitId,
            DayOfWeek = entity.DayOfWeek,
            StartTime = entity.StartTime.ToString(@"hh\:mm"),
            EndTime = entity.EndTime.ToString(@"hh\:mm"),
            IsClosed = entity.IsClosed
        };
    }

    private static RestaurantOperationUnitStatusDto ToStatusDto(RestaurantOperationUnit entity)
    {
        var now = DateTime.Now;

        var todayDayOfWeek = now.DayOfWeek == DayOfWeek.Sunday
            ? 7
            : (int)now.DayOfWeek;

        var today = entity.WorkingHours
            .FirstOrDefault(x => x.DayOfWeek == todayDayOfWeek);

        if (today is null)
        {
            return new RestaurantOperationUnitStatusDto
            {
                OperationUnitId = entity.Id,
                BusinessId = entity.BusinessId,
                UnitType = (int)entity.UnitType,
                UnitTypeText = GetUnitTypeText(entity.UnitType),
                Name = entity.Name,
                IsActive = entity.IsActive,
                HasWorkingHoursForToday = false,
                IsClosedToday = false,
                IsWorkingNow = false,
                StatusText = $"{entity.Name}: radno vreme za danas nije podešeno."
            };
        }

        if (!entity.IsActive)
        {
            return new RestaurantOperationUnitStatusDto
            {
                OperationUnitId = entity.Id,
                BusinessId = entity.BusinessId,
                UnitType = (int)entity.UnitType,
                UnitTypeText = GetUnitTypeText(entity.UnitType),
                Name = entity.Name,
                IsActive = false,
                HasWorkingHoursForToday = true,
                IsClosedToday = today.IsClosed,
                IsWorkingNow = false,
                TodayStartTime = today.StartTime.ToString(@"hh\:mm"),
                TodayEndTime = today.EndTime.ToString(@"hh\:mm"),
                StatusText = $"{entity.Name}: nije aktivno."
            };
        }

        if (today.IsClosed)
        {
            return new RestaurantOperationUnitStatusDto
            {
                OperationUnitId = entity.Id,
                BusinessId = entity.BusinessId,
                UnitType = (int)entity.UnitType,
                UnitTypeText = GetUnitTypeText(entity.UnitType),
                Name = entity.Name,
                IsActive = entity.IsActive,
                HasWorkingHoursForToday = true,
                IsClosedToday = true,
                IsWorkingNow = false,
                TodayStartTime = today.StartTime.ToString(@"hh\:mm"),
                TodayEndTime = today.EndTime.ToString(@"hh\:mm"),
                StatusText = $"{entity.Name}: danas ne radi."
            };
        }

        var currentTime = now.TimeOfDay;

        var isWorkingNow = currentTime >= today.StartTime &&
                           currentTime <= today.EndTime;

        var statusText = isWorkingNow
            ? $"{entity.Name}: trenutno radi do {today.EndTime:hh\\:mm}."
            : $"{entity.Name}: trenutno ne radi. Radno vreme danas: {today.StartTime:hh\\:mm} - {today.EndTime:hh\\:mm}.";

        return new RestaurantOperationUnitStatusDto
        {
            OperationUnitId = entity.Id,
            BusinessId = entity.BusinessId,
            UnitType = (int)entity.UnitType,
            UnitTypeText = GetUnitTypeText(entity.UnitType),
            Name = entity.Name,
            IsActive = entity.IsActive,
            HasWorkingHoursForToday = true,
            IsClosedToday = false,
            IsWorkingNow = isWorkingNow,
            TodayStartTime = today.StartTime.ToString(@"hh\:mm"),
            TodayEndTime = today.EndTime.ToString(@"hh\:mm"),
            StatusText = statusText
        };
    }

    private static string GetUnitTypeText(RestaurantOperationUnitType unitType)
    {
        return unitType switch
        {
            RestaurantOperationUnitType.DiningRoom => "Sala",
            RestaurantOperationUnitType.Kitchen => "Kuhinja",
            RestaurantOperationUnitType.TakeawayCounter => "Šalter / za poneti",
            RestaurantOperationUnitType.Delivery => "Dostava",
            RestaurantOperationUnitType.Reception => "Recepcija / portir",
            RestaurantOperationUnitType.Bar => "Šank",
            RestaurantOperationUnitType.Other => "Ostalo",
            _ => unitType.ToString()
        };
    }
}